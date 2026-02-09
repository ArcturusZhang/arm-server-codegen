import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;
using Azure.ResourceManager;

// ---------------------------------------------------------------------------
// Service definition with API versioning
// ---------------------------------------------------------------------------

@armProviderNamespace
@service(#{ title: "Azure SQL Database Resource Provider" })
@versioned(Versions)
namespace Microsoft.Sql;

/** Supported API versions. */
enum Versions {
  /** Legacy stable version (2021-11-01). Uses Edition/ServiceObjective. */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2021_11_01: "2021-11-01",

  /** Current stable version (2025-08-01). Uses SKU model. */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2025_08_01: "2025-08-01",

  /** Preview version (2025-08-01-preview). Adds enclave and free tier features. */
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  v2025_08_01_preview: "2025-08-01-preview",
}

// ---------------------------------------------------------------------------
// Database resource
// ---------------------------------------------------------------------------

/** SKU information for a database. Introduced in 2025-08-01. */
@added(Versions.v2025_08_01)
model SkuInfo {
  /** The name of the SKU (e.g., S0, GP_S_Gen5_2). */
  name: string;

  /** The tier of the SKU (e.g., Standard, GeneralPurpose). */
  tier?: string;

  /** The family of the SKU (e.g., Gen5). */
  family?: string;

  /** The capacity of the SKU. */
  capacity?: int32;
}

/** Base database properties shared across all versions. */
model DatabaseProperties {
  /** The collation of the database. */
  collation?: string;

  /** The max size of the database in bytes. */
  maxSizeBytes?: int64;

  /** The status of the database. */
  @visibility(Lifecycle.Read)
  status?: string;

  /** The creation date of the database. */
  @visibility(Lifecycle.Read)
  creationDate?: utcDateTime;

  /** The provisioning state. */
  @visibility(Lifecycle.Read)
  provisioningState?: ProvisioningState;

  // --- V2021-11-01 legacy properties ---

  /** The edition of the database (e.g., Standard, Premium). Deprecated in 2025-08-01. */
  @removed(Versions.v2025_08_01)
  edition?: string;

  /** The service objective (e.g., S0, P1). Deprecated in 2025-08-01. */
  @removed(Versions.v2025_08_01)
  serviceObjective?: string;

  // --- V2025-08-01 properties ---

  /** The SKU of the database. Replaces edition/serviceObjective. */
  @added(Versions.v2025_08_01)
  sku?: SkuInfo;

  /** Whether the database is zone redundant. */
  @added(Versions.v2025_08_01)
  zoneRedundant?: string;

  /** The number of high availability replicas. */
  @added(Versions.v2025_08_01)
  highAvailabilityReplicaCount?: int32;

  // --- V2025-08-01-preview properties ---

  /** The preferred enclave type (preview feature). */
  @added(Versions.v2025_08_01_preview)
  preferredEnclaveType?: string;

  /** Whether to use the free database limit (preview feature). */
  @added(Versions.v2025_08_01_preview)
  useFreeLimit?: boolean;
}

/** A SQL Database resource. */
model Database is TrackedResource<DatabaseProperties> {
  /** The name of the database. */
  @key("databaseName")
  @segment("databases")
  @path
  name: string;

  /** The SKU of the database (top-level). */
  @added(Versions.v2025_08_01)
  sku?: SkuInfo;
}

// ---------------------------------------------------------------------------
// Provisioning state
// ---------------------------------------------------------------------------

/** The provisioning state of a resource. */
@lroStatus
union ProvisioningState {
  ResourceProvisioningState,

  /** The resource is being provisioned. */
  Provisioning: "Provisioning",

  /** The resource is being updated. */
  Updating: "Updating",

  /** The resource is being deleted. */
  Deleting: "Deleting",

  /** Provisioning was accepted. */
  Accepted: "Accepted",

  string,
}

// ---------------------------------------------------------------------------
// Operations
// ---------------------------------------------------------------------------

/** Database resource operations. */
@armResourceOperations
interface Databases {
  /** Gets a database. */
  get is ArmResourceRead<Database>;

  /** Creates or updates a database (long-running operation). */
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Database>;

  /** Deletes a database (long-running operation). */
  delete is ArmResourceDeleteWithoutOkAsync<Database>;

  /** Lists databases in a resource group. */
  listByResourceGroup is ArmResourceListByParent<Database>;

  /** Lists databases in a subscription. */
  listBySubscription is ArmListBySubscription<Database>;
}

/** ARM operations endpoint. */
interface Operations extends Azure.ResourceManager.Operations {}
