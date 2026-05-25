using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Architecture.MigrationBlueprints.DualWrite
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
    /// Handles the setup of the target Microservice database before any active data moves.
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
    // PHASE 2: THE DATA MIGRATION (Dual Write Pattern)
    // =========================================================================

    /// <summary>
    /// Orchestrates application-level dual writes and historical backfilling for a zero-downtime database migration.
    /// </summary>
    /// <typeparam name="TEntity">The bounded context entity being migrated (e.g., Order).</typeparam>
    public interface IDualWriteMigrationOrchestrator<TEntity>
    {
        /// <summary>
        /// Step 1: Writes the entity to BOTH the legacy Monolith DB and the new Microservice DB.
        /// </summary>
        /// <param name="entity">The domain entity containing the data to be saved.</param>
        Task WriteToBothDatabasesAsync(TEntity entity);

        /// <summary>
        /// Step 1 (Cont): Retrieves the entity from the legacy Monolith DB to ensure read stability.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to retrieve.</param>
        Task<TEntity> ReadFromMonolithAsync(string id);

        /// <summary>
        /// Step 2: A background worker process to copy historical data that existed prior to dual-writes.
        /// </summary>
        /// <param name="cutoffDate">The exact date/time when dual-writes were activated.</param>
        Task BackfillHistoricalDataAsync(DateTime cutoffDate);

        /// <summary>
        /// Step 3: Compares row counts and max IDs between both databases to ensure 100% synchronization.
        /// </summary>
        /// <returns>True if data perfectly matches; otherwise false.</returns>
        Task<bool> VerifyDataParityAsync();

        /// <summary>
        /// Step 4: Retrieves the entity from the new Microservice DB after parity is confirmed.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to retrieve.</param>
        Task<TEntity> ReadFromMicroserviceDbAsync(string id);

        /// <summary>
        /// Step 5: Removes the dual-write logic and severs the connection to the Monolith DB.
        /// </summary>
        Task FinalizeCutoverAsync();
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