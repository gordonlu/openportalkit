namespace OpenPortalKit.Modules.Dashboard.Storage;

public static class DashboardPostgresSql
{
    public const string InsertAnalyticsEvent = """
        insert into opk_analytics_events (
            id,
            site_id,
            event_type,
            path,
            hashed_session_id,
            occurred_at,
            referrer,
            user_agent,
            anonymized_ip_address,
            is_bot,
            metadata_json
        )
        values (
            @id,
            @site_id,
            @event_type,
            @path,
            @hashed_session_id,
            @occurred_at,
            @referrer,
            @user_agent,
            cast(@anonymized_ip_address as inet),
            @is_bot,
            cast(@metadata_json as jsonb)
        )
        on conflict (id) do update set
            site_id = excluded.site_id,
            event_type = excluded.event_type,
            path = excluded.path,
            hashed_session_id = excluded.hashed_session_id,
            occurred_at = excluded.occurred_at,
            referrer = excluded.referrer,
            user_agent = excluded.user_agent,
            anonymized_ip_address = excluded.anonymized_ip_address,
            is_bot = excluded.is_bot,
            metadata_json = excluded.metadata_json
        """;

    public const string DeleteAnalyticsEventsOlderThan = """
        delete from opk_analytics_events
        where occurred_at < @cutoff
        """;

    public const string InsertDashboardSnapshot = """
        insert into opk_dashboard_snapshots (
            id,
            generated_at,
            created_at,
            expires_at,
            source_checksum,
            summary_json,
            source_modules_json,
            card_count,
            alert_count,
            actionable_alert_count,
            schema_version
        )
        values (
            @id,
            @generated_at,
            @created_at,
            @expires_at,
            @source_checksum,
            cast(@summary_json as jsonb),
            cast(@source_modules_json as jsonb),
            @card_count,
            @alert_count,
            @actionable_alert_count,
            @schema_version
        )
        on conflict (id) do update set
            generated_at = excluded.generated_at,
            created_at = excluded.created_at,
            expires_at = excluded.expires_at,
            source_checksum = excluded.source_checksum,
            summary_json = excluded.summary_json,
            source_modules_json = excluded.source_modules_json,
            card_count = excluded.card_count,
            alert_count = excluded.alert_count,
            actionable_alert_count = excluded.actionable_alert_count,
            schema_version = excluded.schema_version
        """;

    public const string SelectLatestDashboardSnapshot = """
        select
            id,
            summary_json,
            created_at,
            expires_at,
            source_checksum
        from opk_dashboard_snapshots
        order by created_at desc, id desc
        limit 1
        """;
}
