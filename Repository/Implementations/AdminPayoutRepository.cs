using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class AdminPayoutRepository : IAdminPayoutInterface
{
    private readonly NpgsqlConnection _connection;

    public AdminPayoutRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<AdminTransactionLogDto>> GetTransactionLogsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);
            var result = new List<AdminTransactionLogDto>();

            await using var cmd = new NpgsqlCommand("""
                select
                    p.c_payment_id,
                    coalesce(nullif(p.c_transaction_id, ''), concat('TXN-', p.c_payment_id::text)) as transaction_id,
                    coalesce(nullif(buyer.c_full_name, ''), nullif(buyer.c_username, ''), buyer.c_email, 'Unknown Buyer') as buyer_name,
                    coalesce(p.c_amount_paid, 0) as amount_paid,
                    coalesce(nullif(p.c_payment_status, ''), 'Completed') as payment_status,
                    p.c_paid_at
                from t_payment p
                left join t_order o on o.c_order_id = p.c_order_id
                left join t_user buyer on buyer.c_user_id = o.c_buyer_id
                order by p.c_paid_at desc nulls last, p.c_payment_id desc;
                """, _connection);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new AdminTransactionLogDto
                {
                    PaymentId = reader.GetInt32(0),
                    TransactionId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    User = reader.IsDBNull(2) ? "Unknown Buyer" : reader.GetString(2),
                    Amount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    Status = reader.IsDBNull(4) ? "Completed" : reader.GetString(4),
                    Date = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                });
            }

            return result;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<List<AdminPendingPayoutDto>> GetPendingPayoutsAsync(int? artistId = null, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);
            var result = new List<AdminPendingPayoutDto>();

            await using var cmd = new NpgsqlCommand("""
                with artwork_summary as (
                    select
                        date_trunc('month', po.c_paid_at) as request_month,
                        a.c_artist_id,
                        string_agg(distinct coalesce(a.c_title, 'Untitled Artwork'), ', ') as artworks
                    from t_payout po
                    join t_order_item oi on oi.c_order_id = po.c_order_id
                    join t_artwork a on a.c_artwork_id = oi.c_artwork_id
                    where po.c_status = 'Pending'
                    group by date_trunc('month', po.c_paid_at), a.c_artist_id
                ),
                pending_group as (
                    select
                        min(po.c_payout_id) as id,
                        po.c_artist_id,
                        date_trunc('month', po.c_paid_at) as request_month,
                        sum(po.c_gross_amount) as gross_amount,
                        sum(po.c_commission) as commission_amount,
                        sum(po.c_net_amount) as net_amount,
                        count(distinct po.c_order_id)::int as orders_count,
                        max(po.c_paid_at) as requested_at
                    from t_payout po
                    where po.c_status = 'Pending'
                      and (@artist_id::int is null or po.c_artist_id = @artist_id::int)
                    group by po.c_artist_id, date_trunc('month', po.c_paid_at)
                )
                select
                    pg.id,
                    pg.c_artist_id,
                    coalesce(nullif(ap.c_artist_name, ''), nullif(usr.c_full_name, ''), nullif(usr.c_username, ''), usr.c_email, 'Unknown Artist') as artist_name,
                    to_char(pg.request_month, 'Mon YYYY') as request_month_text,
                    coalesce(aws.artworks, 'N/A') as artwork_names,
                    pg.gross_amount,
                    pg.commission_amount,
                    pg.net_amount,
                    pg.orders_count,
                    'Pending'::text as status,
                    pg.requested_at
                from pending_group pg
                left join t_artist_profile ap on ap.c_artist_id = pg.c_artist_id
                left join t_user usr on usr.c_user_id = pg.c_artist_id
                left join artwork_summary aws on aws.request_month = pg.request_month and aws.c_artist_id = pg.c_artist_id
                order by pg.request_month desc, pg.id desc;
                """, _connection);
            var artistParam = new NpgsqlParameter("@artist_id", NpgsqlDbType.Integer)
            {
                Value = artistId.HasValue ? artistId.Value : DBNull.Value
            };
            cmd.Parameters.Add(artistParam);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new AdminPendingPayoutDto
                {
                    Id = reader.GetInt32(0),
                    ArtistId = reader.GetInt32(1),
                    ArtistName = reader.IsDBNull(2) ? "Unknown Artist" : reader.GetString(2),
                    RequestMonth = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Artwork = reader.IsDBNull(4) ? "N/A" : reader.GetString(4),
                    GrossAmount = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    Commission = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    NetAmount = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    OrdersCount = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    Status = reader.IsDBNull(9) ? "Pending" : reader.GetString(9),
                    RequestedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                });
            }

            return result;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<List<AdminPayoutHistoryDto>> GetPayoutHistoryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);
            var result = new List<AdminPayoutHistoryDto>();

            await using var cmd = new NpgsqlCommand("""
                with history_group as (
                    select
                        min(po.c_payout_id) as id,
                        po.c_artist_id,
                        date_trunc('month', po.c_paid_at) as request_month,
                        po.c_status,
                        sum(po.c_net_amount) as amount,
                        max(po.c_paid_at) as processed_date
                    from t_payout po
                    where po.c_status in ('Approved', 'Rejected')
                    group by po.c_artist_id, date_trunc('month', po.c_paid_at), po.c_status
                )
                select
                    hg.id,
                    coalesce(nullif(ap.c_artist_name, ''), nullif(usr.c_full_name, ''), nullif(usr.c_username, ''), usr.c_email, 'Unknown Artist') as artist_name,
                    hg.amount,
                    hg.c_status,
                    hg.processed_date
                from history_group hg
                left join t_artist_profile ap on ap.c_artist_id = hg.c_artist_id
                left join t_user usr on usr.c_user_id = hg.c_artist_id
                order by hg.processed_date desc nulls last, hg.id desc;
                """, _connection);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new AdminPayoutHistoryDto
                {
                    Id = reader.GetInt32(0),
                    ArtistName = reader.IsDBNull(1) ? "Unknown Artist" : reader.GetString(1),
                    Amount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                    Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ProcessedDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }

            return result;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<List<AdminPayoutArtistFilterDto>> GetPendingPayoutArtistsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);
            var result = new List<AdminPayoutArtistFilterDto>();

            await using var cmd = new NpgsqlCommand("""
                select distinct
                    po.c_artist_id,
                    coalesce(nullif(ap.c_artist_name, ''), nullif(u.c_full_name, ''), nullif(u.c_username, ''), u.c_email, 'Unknown Artist') as artist_name
                from t_payout po
                left join t_artist_profile ap on ap.c_artist_id = po.c_artist_id
                left join t_user u on u.c_user_id = po.c_artist_id
                where po.c_status = 'Pending'
                order by artist_name;
                """, _connection);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new AdminPayoutArtistFilterDto
                {
                    ArtistId = reader.GetInt32(0),
                    ArtistName = reader.IsDBNull(1) ? "Unknown Artist" : reader.GetString(1)
                });
            }

            return result;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<AdminPayoutAnalyticsDto> GetPayoutAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);
            var model = new AdminPayoutAnalyticsDto();

            await using (var summaryCmd = new NpgsqlCommand("""
                with payment_summary as (
                    select
                        coalesce(sum(case
                            when lower(coalesce(c_payment_status, 'completed')) in ('completed','success','succeeded','paid')
                            then c_amount_paid else 0 end), 0) as total_revenue,
                        count(case
                            when lower(coalesce(c_payment_status, 'completed')) in ('completed','success','succeeded','paid')
                            then 1 end)::int as successful_txn_count,
                        coalesce(sum(case
                            when lower(coalesce(c_payment_status, '')) in ('failed','refunded')
                            then c_amount_paid else 0 end), 0) as failed_or_refunded_amount
                    from t_payment
                ),
                payout_summary as (
                    select
                        coalesce(sum(case when c_status = 'Pending' then c_net_amount else 0 end), 0) as pending_payout_amount,
                        count(distinct case
                            when c_status = 'Pending'
                            then concat(c_artist_id::text, '-', to_char(date_trunc('month', c_paid_at), 'YYYY-MM'))
                            else null
                        end)::int as pending_payout_count
                    from t_payout
                )
                select
                    ps.total_revenue,
                    ps.successful_txn_count,
                    pos.pending_payout_amount,
                    pos.pending_payout_count,
                    ps.failed_or_refunded_amount
                from payment_summary ps
                cross join payout_summary pos;
                """, _connection))
            await using (var summaryReader = await summaryCmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await summaryReader.ReadAsync(cancellationToken))
                {
                    model.TotalRevenue = summaryReader.IsDBNull(0) ? 0 : summaryReader.GetDecimal(0);
                    model.SuccessfulTransactions = summaryReader.IsDBNull(1) ? 0 : summaryReader.GetInt32(1);
                    model.PendingPayoutAmount = summaryReader.IsDBNull(2) ? 0 : summaryReader.GetDecimal(2);
                    model.PendingPayoutCount = summaryReader.IsDBNull(3) ? 0 : summaryReader.GetInt32(3);
                    model.FailedOrRefundedAmount = summaryReader.IsDBNull(4) ? 0 : summaryReader.GetDecimal(4);
                }
            }

            await using var seriesCmd = new NpgsqlCommand("""
                with months as (
                    select generate_series(
                        date_trunc('month', current_date) - interval '7 month',
                        date_trunc('month', current_date),
                        interval '1 month'
                    ) as m
                )
                select
                    trim(to_char(m.m, 'Mon')) as label,
                    coalesce(sum(case when date_trunc('month', po.c_paid_at) = m.m and po.c_status = 'Approved' then po.c_net_amount else 0 end), 0) as amount
                from months m
                left join t_payout po on date_trunc('month', po.c_paid_at) = m.m
                group by m.m
                order by m.m;
                """, _connection);

            await using var seriesReader = await seriesCmd.ExecuteReaderAsync(cancellationToken);
            while (await seriesReader.ReadAsync(cancellationToken))
            {
                model.MonthlyNetPayoutSeries.Add(new AdminPayoutAnalyticsPointDto
                {
                    Label = seriesReader.IsDBNull(0) ? string.Empty : seriesReader.GetString(0),
                    Value = seriesReader.IsDBNull(1) ? 0 : seriesReader.GetDecimal(1)
                });
            }

            return model;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<bool> ApprovePayoutAsync(int payoutId, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);

            // Bank transfer integration point:
            // replace this status update with actual payout provider transfer call.
            await using var cmd = new NpgsqlCommand("""
                update t_payout tgt
                set
                    c_status = 'Approved',
                    c_paid_at = current_timestamp
                from (
                    select c_artist_id, date_trunc('month', c_paid_at) as request_month
                    from t_payout
                    where c_payout_id = @id and c_status = 'Pending'
                    limit 1
                ) req
                where tgt.c_status = 'Pending'
                  and tgt.c_artist_id = req.c_artist_id
                  and date_trunc('month', tgt.c_paid_at) = req.request_month;
                """, _connection);
            cmd.Parameters.AddWithValue("@id", payoutId);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    public async Task<bool> RejectPayoutAsync(int payoutId, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        try
        {
            await SyncPayoutsFromPaymentsAsync(cancellationToken);

            await using var cmd = new NpgsqlCommand("""
                update t_payout tgt
                set
                    c_status = 'Rejected',
                    c_paid_at = current_timestamp
                from (
                    select c_artist_id, date_trunc('month', c_paid_at) as request_month
                    from t_payout
                    where c_payout_id = @id and c_status = 'Pending'
                    limit 1
                ) req
                where tgt.c_status = 'Pending'
                  and tgt.c_artist_id = req.c_artist_id
                  and date_trunc('month', tgt.c_paid_at) = req.request_month;
                """, _connection);
            cmd.Parameters.AddWithValue("@id", payoutId);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }
        finally
        {
            await CloseIfOpenAsync();
        }
    }

    private async Task SyncPayoutsFromPaymentsAsync(CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand("""
            with artist_per_order as (
                select distinct
                    oi.c_order_id,
                    a.c_artist_id
                from t_order_item oi
                join t_artwork a on a.c_artwork_id = oi.c_artwork_id
                where a.c_artist_id is not null
            ),
            artist_counts as (
                select
                    c_order_id,
                    count(*)::numeric as artist_count
                from artist_per_order
                group by c_order_id
            ),
            payment_artist_split as (
                select
                    p.c_order_id,
                    apo.c_artist_id,
                    coalesce(p.c_amount_paid, 0) / nullif(ac.artist_count, 0) as gross_amount,
                    coalesce(p.c_commission_deducted, 0) / nullif(ac.artist_count, 0) as commission_amount,
                    coalesce(
                        nullif(p.c_artist_payout_amount, 0),
                        coalesce(p.c_amount_paid, 0) - coalesce(p.c_commission_deducted, 0)
                    ) / nullif(ac.artist_count, 0) as net_amount,
                    coalesce(p.c_paid_at, current_timestamp) as paid_at
                from t_payment p
                join artist_per_order apo on apo.c_order_id = p.c_order_id
                join artist_counts ac on ac.c_order_id = p.c_order_id
                where lower(coalesce(p.c_payment_status, 'completed')) in ('completed', 'success', 'succeeded', 'paid')
            ),
            valid_payment_artist_split as (
                select
                    pas.c_order_id,
                    pas.c_artist_id,
                    pas.gross_amount,
                    pas.commission_amount,
                    pas.net_amount,
                    pas.paid_at
                from payment_artist_split pas
                join t_user u on u.c_user_id = pas.c_artist_id
            )
            insert into t_payout (
                c_order_id,
                c_artist_id,
                c_gross_amount,
                c_commission,
                c_net_amount,
                c_status,
                c_paid_at
            )
            select
                vpas.c_order_id,
                vpas.c_artist_id,
                round(vpas.gross_amount, 2),
                round(vpas.commission_amount, 2),
                round(vpas.net_amount, 2),
                'Pending',
                vpas.paid_at
            from valid_payment_artist_split vpas
            left join t_payout po
                on po.c_order_id = vpas.c_order_id
               and po.c_artist_id = vpas.c_artist_id
            where po.c_payout_id is null;

            with artist_per_order as (
                select distinct
                    oi.c_order_id,
                    a.c_artist_id
                from t_order_item oi
                join t_artwork a on a.c_artwork_id = oi.c_artwork_id
                where a.c_artist_id is not null
            ),
            artist_counts as (
                select
                    c_order_id,
                    count(*)::numeric as artist_count
                from artist_per_order
                group by c_order_id
            ),
            payment_artist_split as (
                select
                    p.c_order_id,
                    apo.c_artist_id,
                    coalesce(p.c_amount_paid, 0) / nullif(ac.artist_count, 0) as gross_amount,
                    coalesce(p.c_commission_deducted, 0) / nullif(ac.artist_count, 0) as commission_amount,
                    coalesce(
                        nullif(p.c_artist_payout_amount, 0),
                        coalesce(p.c_amount_paid, 0) - coalesce(p.c_commission_deducted, 0)
                    ) / nullif(ac.artist_count, 0) as net_amount
                from t_payment p
                join artist_per_order apo on apo.c_order_id = p.c_order_id
                join artist_counts ac on ac.c_order_id = p.c_order_id
                where lower(coalesce(p.c_payment_status, 'completed')) in ('completed', 'success', 'succeeded', 'paid')
            ),
            valid_payment_artist_split as (
                select
                    pas.c_order_id,
                    pas.c_artist_id,
                    pas.gross_amount,
                    pas.commission_amount,
                    pas.net_amount
                from payment_artist_split pas
                join t_user u on u.c_user_id = pas.c_artist_id
            )
            update t_payout po
            set
                c_gross_amount = round(vpas.gross_amount, 2),
                c_commission = round(vpas.commission_amount, 2),
                c_net_amount = round(vpas.net_amount, 2)
            from valid_payment_artist_split vpas
            where po.c_order_id = vpas.c_order_id
              and po.c_artist_id = vpas.c_artist_id
              and po.c_status = 'Pending'
              and (
                  po.c_gross_amount <> round(vpas.gross_amount, 2)
                  or po.c_commission <> round(vpas.commission_amount, 2)
                  or po.c_net_amount <> round(vpas.net_amount, 2)
              );
            """, _connection);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }

    private async Task CloseIfOpenAsync()
    {
        if (_connection.State == System.Data.ConnectionState.Open)
        {
            await _connection.CloseAsync();
        }
    }
}
