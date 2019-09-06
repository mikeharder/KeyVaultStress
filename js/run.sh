#!/bin/bash

docker run -it --rm --network host -e AZURE_TENANT_ID -e AZURE_CLIENT_ID -e AZURE_CLIENT_SECRET -e KEY_VAULT_URL -e NODE_TLS_REJECT_UNAUTHORIZED keyvaultstress/js "$@"
