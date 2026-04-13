using System.Globalization;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class AdminOrdersRepository : IAdminOrderInterface
{
    private readonly NpgsqlConnection _connection;

    public AdminOrdersRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<AdminOrdersViewModel> GetOrdersDashboardAsync(CancellationToken cancellationToken = default)
    {
        var model = new AdminOrdersViewModel();

        await EnsureOpenAsync(cancellationToken);

        try
        {
            model.Orders = await LoadOrdersAsync(_connection, cancellationToken);
            model.Categories = await LoadCategoriesAsync(_connection, cancellationToken);

            await using var metricsCommand = new NpgsqlCommand("""
                select
                    coalesce(sum(c_total_amount), 0) as total_revenue,
                    count(*)::int as order_volume,
                    coalesce(avg(c_total_amount), 0) as average_order_value
                from t_order;
                """, _connection);

            await using var metricsReader = await metricsCommand.ExecuteReaderAsync(cancellationToken);
            if (await metricsReader.ReadAsync(cancellationToken))
            {
                model.TotalRevenue = metricsReader.IsDBNull(0) ? 0 : metricsReader.GetDecimal(0);
                model.OrderVolume = metricsReader.IsDBNull(1) ? 0 : metricsReader.GetInt32(1);
                model.AverageOrderValue = metricsReader.IsDBNull(2) ? 0 : metricsReader.GetDecimal(2);
            }
        }
        finally
        {
            await CloseIfOpenAsync();
        }

        return model;
    }

    public async Task<AdminAnalyticsViewModel> GetAnalyticsDashboardAsync(string? period = null, CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizePeriod(period);
        var model = new AdminAnalyticsViewModel
        {
            SelectedPeriod = normalizedPeriod,
            DateRangeText = GetDateRangeText(normalizedPeriod)
        };

        await EnsureOpenAsync(cancellationToken);

        try
        {
            await LoadAnalyticsSummaryAsync(_connection, model, normalizedPeriod, cancellationToken);
            model.RevenueGrowth = await LoadRevenueGrowthAsync(_connection, normalizedPeriod, cancellationToken);
            model.UserActivity = await LoadUserActivityAsync(_connection, normalizedPeriod, cancellationToken);
            model.TopPerformingArtworks = await LoadTopPerformingArtworksAsync(_connection, cancellationToken);
        }
        finally
        {
            await CloseIfOpenAsync();
        }

        return model;
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

    private static async Task LoadAnalyticsSummaryAsync(
        NpgsqlConnection connection,
        AdminAnalyticsViewModel model,
        string period,
        CancellationToken cancellationToken)
    {
        var query = period switch
        {
            "day" => """
                with completed_orders as (
                    select c_total_amount, c_buyer_id, c_created_at
                    from t_order
                    where coalesce(c_order_status, 'Completed') = 'Completed'
                ),
                current_period_orders as (
                    select * from completed_orders
                    where date_trunc('day', c_created_at) = date_trunc('day', current_timestamp)
                ),
                previous_period_orders as (
                    select * from completed_orders
                    where date_trunc('day', c_created_at) = date_trunc('day', current_timestamp - interval '1 day')
                ),
                payment_totals as (
                    select
                        coalesce(sum(c_artist_payout_amount), 0) as total_payout,
                        coalesce(sum(c_commission_deducted), 0) as total_commission,
                        coalesce(sum(case when date_trunc('day', coalesce(c_paid_at, current_timestamp)) = date_trunc('day', current_timestamp) then c_artist_payout_amount else 0 end), 0) as current_payout,
                        coalesce(sum(case when date_trunc('day', coalesce(c_paid_at, current_timestamp)) = date_trunc('day', current_timestamp - interval '1 day') then c_artist_payout_amount else 0 end), 0) as previous_payout
                    from t_payment
                )
                select
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as total_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from current_period_orders), 0)::int as active_buyers,
                    (select total_payout from payment_totals) as total_payout,
                    (select total_commission from payment_totals) as total_commission,
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as current_revenue,
                    coalesce((select sum(c_total_amount) from previous_period_orders), 0) as previous_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as current_avg_order_value,
                    coalesce((select avg(c_total_amount) from previous_period_orders), 0) as previous_avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from previous_period_orders), 0)::int as previous_active_buyers,
                    (select current_payout from payment_totals) as current_payout,
                    (select previous_payout from payment_totals) as previous_payout;
                """,
            "week" => """
                with completed_orders as (
                    select c_total_amount, c_buyer_id, c_created_at
                    from t_order
                    where coalesce(c_order_status, 'Completed') = 'Completed'
                ),
                current_period_orders as (
                    select * from completed_orders
                    where date_trunc('week', c_created_at) = date_trunc('week', current_timestamp)
                ),
                previous_period_orders as (
                    select * from completed_orders
                    where date_trunc('week', c_created_at) = date_trunc('week', current_timestamp - interval '1 week')
                ),
                payment_totals as (
                    select
                        coalesce(sum(c_artist_payout_amount), 0) as total_payout,
                        coalesce(sum(c_commission_deducted), 0) as total_commission,
                        coalesce(sum(case when date_trunc('week', coalesce(c_paid_at, current_timestamp)) = date_trunc('week', current_timestamp) then c_artist_payout_amount else 0 end), 0) as current_payout,
                        coalesce(sum(case when date_trunc('week', coalesce(c_paid_at, current_timestamp)) = date_trunc('week', current_timestamp - interval '1 week') then c_artist_payout_amount else 0 end), 0) as previous_payout
                    from t_payment
                )
                select
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as total_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from current_period_orders), 0)::int as active_buyers,
                    (select total_payout from payment_totals) as total_payout,
                    (select total_commission from payment_totals) as total_commission,
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as current_revenue,
                    coalesce((select sum(c_total_amount) from previous_period_orders), 0) as previous_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as current_avg_order_value,
                    coalesce((select avg(c_total_amount) from previous_period_orders), 0) as previous_avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from previous_period_orders), 0)::int as previous_active_buyers,
                    (select current_payout from payment_totals) as current_payout,
                    (select previous_payout from payment_totals) as previous_payout;
                """,
            _ => """
                with completed_orders as (
                    select c_total_amount, c_buyer_id, c_created_at
                    from t_order
                    where coalesce(c_order_status, 'Completed') = 'Completed'
                ),
                current_period_orders as (
                    select * from completed_orders
                    where date_trunc('month', c_created_at) = date_trunc('month', current_date)
                ),
                previous_period_orders as (
                    select * from completed_orders
                    where date_trunc('month', c_created_at) = date_trunc('month', current_date - interval '1 month')
                ),
                payment_totals as (
                    select
                        coalesce(sum(c_artist_payout_amount), 0) as total_payout,
                        coalesce(sum(c_commission_deducted), 0) as total_commission,
                        coalesce(sum(case when date_trunc('month', coalesce(c_paid_at, current_timestamp)) = date_trunc('month', current_date) then c_artist_payout_amount else 0 end), 0) as current_payout,
                        coalesce(sum(case when date_trunc('month', coalesce(c_paid_at, current_timestamp)) = date_trunc('month', current_date - interval '1 month') then c_artist_payout_amount else 0 end), 0) as previous_payout
                    from t_payment
                )
                select
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as total_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from current_period_orders), 0)::int as active_buyers,
                    (select total_payout from payment_totals) as total_payout,
                    (select total_commission from payment_totals) as total_commission,
                    coalesce((select sum(c_total_amount) from current_period_orders), 0) as current_revenue,
                    coalesce((select sum(c_total_amount) from previous_period_orders), 0) as previous_revenue,
                    coalesce((select avg(c_total_amount) from current_period_orders), 0) as current_avg_order_value,
                    coalesce((select avg(c_total_amount) from previous_period_orders), 0) as previous_avg_order_value,
                    coalesce((select count(distinct c_buyer_id) from previous_period_orders), 0)::int as previous_active_buyers,
                    (select current_payout from payment_totals) as current_payout,
                    (select previous_payout from payment_totals) as previous_payout;
                """
        };

        await using var command = new NpgsqlCommand(query, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return;
        }

        model.TotalRevenue = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
        model.AverageOrderValue = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
        model.ActiveBuyers = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        model.TotalArtistPayout = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
        model.TotalCommission = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);

        var currentRevenue = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
        var previousRevenue = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6);
        var currentAverageOrderValue = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7);
        var previousAverageOrderValue = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8);
        var previousActiveBuyers = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
        var currentPayout = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10);
        var previousPayout = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11);

        model.RevenueChangePercentage = CalculatePercentageChange(currentRevenue, previousRevenue);
        model.AverageOrderValueChangePercentage = CalculatePercentageChange(currentAverageOrderValue, previousAverageOrderValue);
        model.ActiveBuyersChangePercentage = CalculatePercentageChange(model.ActiveBuyers, previousActiveBuyers);
        model.PayoutChangePercentage = CalculatePercentageChange(currentPayout, previousPayout);
    }

    private static async Task<List<AdminRevenuePointViewModel>> LoadRevenueGrowthAsync(
        NpgsqlConnection connection,
        string period,
        CancellationToken cancellationToken)
    {
        var points = new List<AdminRevenuePointViewModel>();

        var query = period switch
        {
            "day" => """
                with points as (
                    select generate_series(
                        date_trunc('day', current_timestamp) - interval '6 day',
                        date_trunc('day', current_timestamp),
                        interval '1 day'
                    ) as point_start
                )
                select
                    to_char(p.point_start, 'DD Mon') as point_label,
                    coalesce(sum(o.c_total_amount), 0) as revenue
                from points p
                left join t_order o
                    on date_trunc('day', o.c_created_at) = p.point_start
                   and coalesce(o.c_order_status, 'Completed') = 'Completed'
                group by p.point_start
                order by p.point_start;
                """,
            "week" => """
                with points as (
                    select generate_series(
                        date_trunc('week', current_timestamp) - interval '7 week',
                        date_trunc('week', current_timestamp),
                        interval '1 week'
                    ) as point_start
                )
                select
                    to_char(p.point_start, 'DD Mon') as point_label,
                    coalesce(sum(o.c_total_amount), 0) as revenue
                from points p
                left join t_order o
                    on date_trunc('week', o.c_created_at) = p.point_start
                   and coalesce(o.c_order_status, 'Completed') = 'Completed'
                group by p.point_start
                order by p.point_start;
                """,
            _ => """
                with points as (
                    select generate_series(
                        date_trunc('year', current_date),
                        date_trunc('year', current_date) + interval '11 month',
                        interval '1 month'
                    ) as point_start
                )
                select
                    trim(to_char(p.point_start, 'Mon')) as point_label,
                    coalesce(sum(o.c_total_amount), 0) as revenue
                from points p
                left join t_order o
                    on date_trunc('month', o.c_created_at) = p.point_start
                   and coalesce(o.c_order_status, 'Completed') = 'Completed'
                group by p.point_start
                order by p.point_start;
                """
        };

        await using var command = new NpgsqlCommand(query, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new AdminRevenuePointViewModel
            {
                Label = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Revenue = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1)
            });
        }

        return points;
    }

    private static async Task<List<AdminUserActivityPointViewModel>> LoadUserActivityAsync(
        NpgsqlConnection connection,
        string period,
        CancellationToken cancellationToken)
    {
        var points = new List<AdminUserActivityPointViewModel>();

        var query = period switch
        {
            "day" => """
                with points as (
                    select generate_series(
                        date_trunc('day', current_timestamp) - interval '6 day',
                        date_trunc('day', current_timestamp),
                        interval '1 day'
                    ) as point_start
                )
                select
                    to_char(p.point_start, 'DD Mon') as point_label,
                    coalesce(count(distinct case when date_trunc('day', u.c_created_at) = p.point_start then u.c_user_id end), 0)::int as new_users,
                    coalesce(count(distinct case when date_trunc('day', o.c_created_at) = p.point_start and coalesce(o.c_order_status, 'Completed') = 'Completed' then o.c_buyer_id end), 0)::int as active_users
                from points p
                left join t_user u on date_trunc('day', u.c_created_at) = p.point_start
                left join t_order o on date_trunc('day', o.c_created_at) = p.point_start
                group by p.point_start
                order by p.point_start;
                """,
            "week" => """
                with points as (
                    select generate_series(
                        date_trunc('week', current_timestamp) - interval '7 week',
                        date_trunc('week', current_timestamp),
                        interval '1 week'
                    ) as point_start
                )
                select
                    to_char(p.point_start, 'DD Mon') as point_label,
                    coalesce(count(distinct case when date_trunc('week', u.c_created_at) = p.point_start then u.c_user_id end), 0)::int as new_users,
                    coalesce(count(distinct case when date_trunc('week', o.c_created_at) = p.point_start and coalesce(o.c_order_status, 'Completed') = 'Completed' then o.c_buyer_id end), 0)::int as active_users
                from points p
                left join t_user u on date_trunc('week', u.c_created_at) = p.point_start
                left join t_order o on date_trunc('week', o.c_created_at) = p.point_start
                group by p.point_start
                order by p.point_start;
                """,
            _ => """
                with points as (
                    select generate_series(
                        date_trunc('month', current_date) - interval '3 month',
                        date_trunc('month', current_date),
                        interval '1 month'
                    ) as point_start
                )
                select
                    trim(to_char(p.point_start, 'Mon')) as point_label,
                    coalesce(count(distinct case when date_trunc('month', u.c_created_at) = p.point_start then u.c_user_id end), 0)::int as new_users,
                    coalesce(count(distinct case when date_trunc('month', o.c_created_at) = p.point_start and coalesce(o.c_order_status, 'Completed') = 'Completed' then o.c_buyer_id end), 0)::int as active_users
                from points p
                left join t_user u on date_trunc('month', u.c_created_at) = p.point_start
                left join t_order o on date_trunc('month', o.c_created_at) = p.point_start
                group by p.point_start
                order by p.point_start;
                """
        };

        await using var command = new NpgsqlCommand(query, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new AdminUserActivityPointViewModel
            {
                Label = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                NewUsers = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                ActiveUsers = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
            });
        }

        return points;
    }

    private static async Task<List<AdminTopArtworkViewModel>> LoadTopPerformingArtworksAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var artworks = new List<AdminTopArtworkViewModel>();

        await using var command = new NpgsqlCommand("""
            select
                a.c_title,
                coalesce(ap.c_artist_name, nullif(u.c_full_name, ''), nullif(u.c_username, ''), 'Unknown Artist') as artist_name,
                count(oi.c_item_id)::int as sales_count,
                coalesce(sum(oi.c_price_at_purchase), 0) as total_revenue,
                coalesce(c.c_category_name, 'Uncategorized') as category_name
            from t_order_item oi
            join t_order o on o.c_order_id = oi.c_order_id
            join t_artwork a on a.c_artwork_id = oi.c_artwork_id
            left join t_artist_profile ap on ap.c_artist_id = a.c_artist_id
            left join t_user u on u.c_user_id = a.c_artist_id
            left join t_category c on c.c_category_id = a.c_category_id
            where coalesce(o.c_order_status, 'Completed') = 'Completed'
            group by a.c_artwork_id, a.c_title, ap.c_artist_name, u.c_full_name, u.c_username, c.c_category_name
            order by total_revenue desc, sales_count desc, a.c_title
            limit 4;
            """, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var revenue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
            var category = reader.IsDBNull(4) ? "Uncategorized" : reader.GetString(4);

            artworks.Add(new AdminTopArtworkViewModel
            {
                ArtworkTitle = reader.IsDBNull(0) ? "Untitled Artwork" : reader.GetString(0),
                ArtistName = reader.IsDBNull(1) ? "Unknown Artist" : reader.GetString(1),
                SalesCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Revenue = revenue,
                RevenueText = revenue.ToString("C", CultureInfo.GetCultureInfo("en-US")),
                ArtworkTone = GetArtworkTone(category)
            });
        }

        return artworks;
    }

    private static async Task<List<AdminOrderRowViewModel>> LoadOrdersAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var orders = new List<AdminOrderRowViewModel>();
        var seenOrderIds = new HashSet<int>();

        await using var command = new NpgsqlCommand("""
            with item_summary as (
                select
                    oi.c_order_id,
                    string_agg(coalesce(a.c_title, 'Artwork unavailable'), ', ' order by oi.c_item_id) as artwork_titles,
                    string_agg(distinct coalesce(c.c_category_name, 'Uncategorized'), ', ') as category_names
                from t_order_item oi
                left join t_artwork a on a.c_artwork_id = oi.c_artwork_id
                left join t_category c on c.c_category_id = a.c_category_id
                group by oi.c_order_id
            )
            select
                o.c_order_id,
                o.c_total_amount,
                o.c_order_status,
                o.c_created_at,
                coalesce(nullif(u.c_full_name, ''), nullif(u.c_username, ''), u.c_email, 'Unknown Buyer') as buyer_name,
                coalesce(item.artwork_titles, 'Artwork unavailable') as artwork_name,
                coalesce(item.category_names, 'Uncategorized') as artwork_type
            from t_order o
            left join t_user u on u.c_user_id = o.c_buyer_id
            left join item_summary item on item.c_order_id = o.c_order_id
            order by o.c_created_at desc nulls last, o.c_order_id desc;
            """, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetInt32(0);
            if (!seenOrderIds.Add(orderId))
            {
                continue;
            }

            var amount = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
            var status = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
            var createdAt = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
            var buyerName = reader.IsDBNull(4) ? "Unknown Buyer" : reader.GetString(4);
            var artworkName = reader.IsDBNull(5) ? "Artwork unavailable" : reader.GetString(5);
            var artworkType = reader.IsDBNull(6) ? "Uncategorized" : reader.GetString(6);

            orders.Add(new AdminOrderRowViewModel
            {
                Id = $"#ORD-{orderId:D5}",
                BuyerInitials = GetInitials(buyerName),
                BuyerName = buyerName,
                BuyerTone = GetBuyerTone(orderId),
                ArtworkName = artworkName,
                ArtworkType = artworkType,
                ArtworkTone = GetArtworkTone(artworkType),
                Amount = amount,
                AmountText = amount.ToString("C", CultureInfo.GetCultureInfo("en-US")),
                Status = status,
                CreatedAt = createdAt
            });
        }

        return orders;
    }

    private static async Task<List<string>> LoadCategoriesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var categories = new List<string>();

        await using var command = new NpgsqlCommand("""
            select c_category_name
            from t_category
            where nullif(trim(c_category_name), '') is not null
            order by c_category_name;
            """, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                categories.Add(reader.GetString(0));
            }
        }

        return categories;
    }

    private static string GetInitials(string value)
    {
        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]).ToString());

        var initials = string.Concat(parts);
        return string.IsNullOrWhiteSpace(initials) ? "NA" : initials;
    }

    private static string GetBuyerTone(int orderId) => orderId % 2 == 0 ? string.Empty : "cool";

    private static string NormalizePeriod(string? period) => period?.Trim().ToLowerInvariant() switch
    {
        "day" => "day",
        "week" => "week",
        _ => "month"
    };

    private static string GetDateRangeText(string period)
    {
        var now = DateTime.Now;

        if (period == "day")
        {
            return now.ToString("dd MMM yyyy");
        }

        if (period == "week")
        {
            var start = StartOfWeek(now);
            var end = start.AddDays(6);
            return $"{start:dd MMM yyyy} - {end:dd MMM yyyy}";
        }

        return $"Jan 1, {now.Year} - Dec 31, {now.Year}";
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static decimal CalculatePercentageChange(decimal currentValue, decimal previousValue)
    {
        if (previousValue == 0)
        {
            return currentValue == 0 ? 0 : 100;
        }

        return Math.Round(((currentValue - previousValue) / previousValue) * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculatePercentageChange(int currentValue, int previousValue)
    {
        if (previousValue == 0)
        {
            return currentValue == 0 ? 0 : 100;
        }

        return Math.Round(((decimal)(currentValue - previousValue) / previousValue) * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private static string GetArtworkTone(string category) => category.ToLowerInvariant() switch
    {
        "oil on canvas" => "peach",
        "digital art" => "sand",
        "watercolor" => "landscape",
        "sculpture" => "night",
        "mixed media" => "gold",
        _ => "peach"
    };
}
