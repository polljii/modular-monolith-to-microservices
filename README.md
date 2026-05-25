# Modular Monolith to Microservices Migration Blueprints

A collection of architectural blueprints and C# interface representations demonstrating how to safely extract microservices from a modular monolith, specifically focusing on the hardest part: **the database split**.

## 📌 The Challenge
When moving from a modular monolith to microservices, the code decoupling is usually straightforward if your bounded contexts are well-defined. The real challenge is achieving a **Database-per-Service** architecture without duplicating massive amounts of data or causing severe production downtime. 

This repository outlines the step-by-step strategies to achieve this safely.

## 🚀 Migration Strategies Included

This repository contains C# abstractions for two distinct zero-downtime migration paths:

### 1. The Application-Driven Path (Dual Writes)
Located in `Architecture.MigrationBlueprints.DualWrite`
* **Best for:** Teams with strict zero-downtime requirements but limited access to advanced infrastructure tooling.
* **How it works:** The application logic handles writing to both the legacy monolith database and the new microservice database simultaneously while a background worker backfills historical data.

### 2. The Infrastructure-Driven Path (Change Data Capture - CDC)
Located in `Architecture.MigrationBlueprints.ChangeDataCapture`
* **Best for:** Cloud-native teams (AWS/Azure) who want to keep migration logic entirely out of their application codebase.
* **How it works:** Relies on infrastructure tools (like AWS DMS, Azure DMS, or Debezium) to read database transaction logs and continuously stream data to the new microservice database until a hot cutover is performed.

## 📖 The 3-Phase Migration Blueprint

Regardless of the data migration strategy chosen, the blueprints follow a strict 3-phase approach:

1.  **Phase 1: Code & Logic Decoupling**
    * Clone the repository and isolate the code.
    * Replace cross-context SQL `JOIN`s with application-level API merges.
    * Provision the empty target database and seed static reference data.
2.  **Phase 2: The Data Migration**
    * Execute either the *Dual Write* or *CDC* strategy to sync transactional data.
    * Verify data parity between the monolith and the target database.
    * Execute the network/routing cutover.
3.  **Phase 3: The Great Purge**
    * Wait for system stability (2-4 weeks).
    * Drop the unused tables from the new microservice database.
    * Drop the migrated tables from the legacy monolith database.
    * Delete dead module code from the new microservice codebase.

## 🛠️ Repository Structure

* `/src/DualWrite/` - Contains interfaces and abstract managers for the Dual Write pattern.
* `/src/CDC/` - Contains interfaces and abstract orchestrators for the Change Data Capture pattern.
* `/src/Shared/` - Contains the aggregator and provisioner patterns shared across both strategies.

*(Note to users: These are architectural templates and interfaces, not highly opinionated implementation libraries. Use them as a structural guide for your own domain).*

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
