namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class AgentOutputPostgresSql
{
    public const string UpsertArtifact = """
        insert into opk_agent_output_artifacts (
            path,
            content_type,
            body,
            source_id,
            source_kind,
            schema_version,
            checksum,
            generated_at
        )
        values (
            @path,
            @content_type,
            @body,
            @source_id,
            @source_kind,
            @schema_version,
            @checksum,
            @generated_at
        )
        on conflict (path) do update set
            content_type = excluded.content_type,
            body = excluded.body,
            source_id = excluded.source_id,
            source_kind = excluded.source_kind,
            schema_version = excluded.schema_version,
            checksum = excluded.checksum,
            generated_at = excluded.generated_at,
            updated_at = now()
        """;

    public const string SelectArtifactByPath = """
        select
            path,
            content_type,
            body,
            source_id,
            source_kind,
            schema_version,
            checksum,
            generated_at
        from opk_agent_output_artifacts
        where path = @path
        """;

    public const string SelectArtifacts = """
        select
            path,
            content_type,
            body,
            source_id,
            source_kind,
            schema_version,
            checksum,
            generated_at
        from opk_agent_output_artifacts
        order by path asc
        """;
}
