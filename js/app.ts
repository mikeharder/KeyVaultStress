import { performance } from 'perf_hooks';
import commander from "commander";
import { DefaultAzureCredential, TokenCredential, GetTokenOptions, AccessToken } from "@azure/identity";
import { SecretsClient } from "@azure/keyvault-secrets";

async function main(): Promise<void> {
  commander
    .option('-i, --iterations <iterations>', 'number of iterations', 10)
    .option('-d, --delete', 'delete secret between iterations', false)
    .option('-m, --mockCredential', 'use mock credential instead of Azure AD', false)
    .option('-n, --newClientPerIteration', 'create new client for every iteration', false);

  commander.parse(process.argv);

  const url = process.env.KEY_VAULT_URL;
  if (!url) {
    throw 'Env var KEY_VAULT_URL must be set';
  }

  class MockCredential implements TokenCredential {
    async getToken(scopes: string | string[], options?: GetTokenOptions): Promise<AccessToken | null> {
      return {
        token: "test-token",
        expiresOnTimestamp: 9999999999
      };
    }
  }

  let credential: TokenCredential;
  if (commander.mockCredential) {
    credential = new MockCredential();
  }
  else {
    // DefaultAzureCredential expects the following three environment variables:
    // - AZURE_TENANT_ID: The tenant ID in Azure Active Directory
    // - AZURE_CLIENT_ID: The application (client) ID registered in the AAD tenant
    // - AZURE_CLIENT_SECRET: The client secret for the registered application
    credential = new DefaultAzureCredential();
  }

  const secretName = "TestSecret";
  const value = "TestValue";

  let client = new SecretsClient(url, credential);
  for (let i = 0; i < commander.iterations; i++) {
    if (commander.newClientPerIteration) {
      client = new SecretsClient(url, credential);
    }

    const startMs = performance.now();
    await client.setSecret(secretName, value);
    const result = await client.getSecret(secretName);
    const endMs = performance.now();

    const elapsedMs = endMs - startMs;
    log(`${i} ${result.value} ${Math.round(elapsedMs)}ms`);

    if (commander.delete) {
      await client.deleteSecret(secretName);
    }
  }
}

function log(message: string) {
  console.log(`[${new Date().toISOString()}] ${message}`)
}

main().catch(err => {
  console.log('Error occurred: ', err);
});
