using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class PaymentRepository : IPaymentInterface
{
    private readonly string _connectionString;

    public PaymentRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("pgconn");
    }

    public async Task<int> ProcessFullPaymentAsync(int buyerId, t_PaymentVerify model)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            decimal total = model.Cart.Sum(x => x.Price);
            decimal commission = Math.Round(total * 0.20M, 2);
            decimal payout = total - commission;

            // ── INSERT ORDER ─────────────────────────────────────────────
            await using var orderCmd = new NpgsqlCommand(@"
                INSERT INTO t_order (c_buyer_id, c_total_amount, c_order_status)
                VALUES (@buyer, @amount, 'Completed')
                RETURNING c_order_id", conn, transaction);

            orderCmd.Parameters.Add("@buyer", NpgsqlDbType.Integer).Value = buyerId;
            orderCmd.Parameters.Add("@amount", NpgsqlDbType.Numeric).Value = total;

            int orderId = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());
 
            // ── INSERT ORDER ITEMS ───────────────────────────────────────
            foreach (var item in model.Cart)
            {
                // ✅ Validate artwork exists (DO NOT INSERT)
                await using var checkCmd = new NpgsqlCommand(@"
                    SELECT COUNT(1) 
                    FROM t_artwork 
                    WHERE c_artwork_id = @id", conn, transaction);

                checkCmd.Parameters.Add("@id", NpgsqlDbType.Integer).Value = item.ArtworkId;

                var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                if (exists == 0)
                    throw new Exception($"Artwork {item.ArtworkId} does not exist");

                await using var itemCmd = new NpgsqlCommand(@"
                    INSERT INTO t_order_item 
                        (c_order_id, c_artwork_id, c_price_at_purchase)
                    VALUES 
                        (@orderId, @artworkId, @price)", conn, transaction);

                itemCmd.Parameters.Add("@orderId", NpgsqlDbType.Integer).Value = orderId;
                itemCmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = item.ArtworkId;
                itemCmd.Parameters.Add("@price", NpgsqlDbType.Numeric).Value = item.Price;

                await itemCmd.ExecuteNonQueryAsync();
            }

            // ── INSERT PAYMENT ───────────────────────────────────────────
            await using var payCmd = new NpgsqlCommand(@"
                INSERT INTO t_payment
                    (c_order_id, c_transaction_id, c_method,
                     c_amount_paid, c_commission_deducted, c_artist_payout_amount,
                     c_payment_status, c_currency)
                VALUES
                    (@orderId, @txnId, 'PAYPAL',
                     @total, @commission, @payout,
                     'SUCCESS', @currency)", conn, transaction);

            payCmd.Parameters.Add("@orderId", NpgsqlDbType.Integer).Value = orderId;
            payCmd.Parameters.Add("@txnId", NpgsqlDbType.Varchar).Value = model.PaypalOrderId;
            payCmd.Parameters.Add("@total", NpgsqlDbType.Numeric).Value = total;
            payCmd.Parameters.Add("@commission", NpgsqlDbType.Numeric).Value = commission;
            payCmd.Parameters.Add("@payout", NpgsqlDbType.Numeric).Value = payout;
            payCmd.Parameters.Add("@currency", NpgsqlDbType.Varchar).Value = model.Currency ?? "USD";

            await payCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return orderId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}