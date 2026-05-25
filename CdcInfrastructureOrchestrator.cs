using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Architecture.MigrationBlueprints.ChangeDataCapture
{
    // =========================================================================
    // PHASE 1: CODE & LOGIC DECOUPLING (Shared Strategy)
    // =========================================================================

    /// <summary>
    /// Replaces cross-context SQL JOINs. Instead of joining tables in the database, 
    /// fetch them separately and merge them in memory to break physical database dependencies.
    /// </summary>
    /// <typeparam name="TAggregate">The final combined object needed by the application.</typeparam>
    /// <typeparam name="TContextA">The primary domain entity (e.g., Order).</typeparam>
    /// <typeparam name="TContextB">The external domain entity (e.g., Customer).</typeparam>
    public interface ICrossContextAggregator<TAggregate, TContextA, TContextB>
    {
        /// <summary>
        /// Retrieves the primary entity from the local context's database.
        /// </summary>
        /// <param name="id">The unique identifier of the primary entity.</param>
        /// <returns>The primary domain entity.</returns>
        Task<TContextA> FetchFromPrimaryDomainAsync(string id);

        /// <summary>
        /// Retrieves the related entity from an external context using an HTTP client or internal service call.
        /// </summary>
        /// <param name="externalId">The foreign key or external identifier.</param>
        /// <returns>The external domain entity.</returns>
        Task<TContextB> FetchFromExternalDomainViaHttpAsync(string externalId);

        /// <summary>
        /// Combines the primary and external entities into a single aggregate object for the client.
        /// </summary>
        /// <param name="entityA">The primary entity data.</param>
        /// <param name="entityB">The external entity data.</param>
        /// <returns>The fully hydrated aggregate object.</returns>
        TAggregate MergeInMemory(TContextA entityA, TContextB entityB);
    }

    /// <summary>
    /// Handles the setup of the target Microservice database before replication begins.
    /// </summary>
    public interface IDatabaseProvisioner
    {
        /// <summary>
        /// Executes DDL scripts to create the necessary tables, schemas, and indices in the new database.
        /// </summary>
        /// <param name="targetConnectionString">The connection string for the new Microservice database.</param>
        Task GenerateEmptySchemaAsync(string targetConnectionString);

        /// <summary>
        /// Populates static lookup tables (e.g., country codes, statuses) that are safe to duplicate.
        /// </summary>
        /// <param name="targetConnectionString">The connection string for the new Microservice database.</param>
        /// <param name="lookupData">The static reference data to insert.</param>
        Task SeedStaticReferenceDataAsync(string targetConnectionString, IEnumerable<object> lookupData);
    }

    // =========================================================================
    // PHASE 2: THE DATA MIGRATION (CDC Pattern)
    // =========================================================================

    /// <summary>
    /// Represents the orchestration of external infrastructure tools (like AWS DMS or Debezium).
    /// The application code DOES NOT move the data here; it only observes and reroutes.
    /// </summary>
    public interface ICdcInfrastructureOrchestrator
    {
        /// <summary>
        /// Step 1: Triggers the cloud provider's API to provision and start the CDC replication instance.
        /// </summary>
        /// <param name="sourceDb">The legacy Monolith database connection identifier.</param>
        /// <param name="targetDb">The new Microservice database connection identifier.</param>
        /// <param name="tablesToMigrate">An array of table names specifically belonging to this bounded context.</param>
        Task StartCdcReplicationAsync(string sourceDb, string targetDb, string[] tablesToMigrate);

        /// <summary>
        /// Step 2: Queries the CDC tool's API to check how far behind the target database is. Goal is 0 seconds.
        /// </summary>
        /// <returns>The current replication delay.</returns>
        Task<TimeSpan> CheckReplicationLagAsync();

        /// <summary>
        /// Step 2 (Cont): Validates that the CDC tool successfully moved all historical data without dropping rows.
        /// </summary>
        /// <returns>True if data matches perfectly; otherwise false.</returns>
        Task<bool> VerifyDataParityAsync();

        /// <summary>
        /// Step 3: Updates network routing (e.g., API Gateway) to point traffic to the new Microservice environment.
        /// </summary>
        /// <param name="newMicroserviceEndpoint">The base URL of the newly deployed independent Microservice.</param>
        Task ExecuteTrafficCutoverViaApiGatewayAsync(string newMicroserviceEndpoint);

        /// <summary>
        /// Step 4: Signals the infrastructure to tear down the CDC replication service, severing the DB ties.
        /// </summary>
        Task StopAndTearDownCdcReplicationAsync();
    }

    /// <summary>
    /// Manages application-level connection string toggling during the Hot Cutover phase.
    /// </summary>
    public interface IDatabaseConnectionManager
    {
        /// <summary>
        /// Dynamically resolves the active connection string based on the cutover state.
        /// </summary>
        /// <param name="isCutoverComplete">False routes to the Monolith DB. True routes to the Microservice DB.</param>
        /// <returns>The active connection string.</returns>
        string GetActiveConnectionString(bool isCutoverComplete);
    }

    // =========================================================================
    // PHASE 3: THE GREAT PURGE
    // =========================================================================

    /// <summary>
    /// Represents the cleanup tasks executed 2-4 weeks after a successful cutover.
    /// </summary>
    public interface ILegacyCleanupManager
    {
        /// <summary>
        /// Drops the migrated tables from the legacy Monolith database once they are no longer queried.
        /// </summary>
        /// <param name="migratedTables">A list of table names to drop.</param>
        Task DropGhostTablesFromMonolithDbAsync(IEnumerable<string> migratedTables);

        /// <summary>
        /// Drops the empty tables belonging to other modules from the newly cloned Microservice database.
        /// </summary>
        /// <param name="unusedTables">A list of table names to drop.</param>
        Task DropUnusedMonolithTablesFromMicroserviceDbAsync(IEnumerable<string> unusedTables);
        
        /// <summary>
        /// A marker method reminding developers to delete unused bounded context folders from the source code.
        /// </summary>
        [Obsolete("Delete dead module code from the new Microservice repository.", error: true)]
        void RemoveUnusedBoundedContextsFromCodebase();
    }
}