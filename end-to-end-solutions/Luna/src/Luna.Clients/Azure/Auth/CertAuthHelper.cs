using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Luna.Clients.Exceptions;

namespace Luna.Clients.Azure.Auth
{
    public class CertAuthHelper
    {
        private readonly IKeyVaultHelper _keyVaultHelper;

        public CertAuthHelper(IKeyVaultHelper keyVaultHelper)
        {
            _keyVaultHelper = keyVaultHelper;
        }

        public async void VerifyUserAccess(string clientCert, ILogger logger)
        {
            string sysCert = await _keyVaultHelper.GetSecretAsync("lunaaitest-keyvault", "cert-thumbprint");
            if (clientCert != sysCert || string.IsNullOrEmpty(clientCert))
            {
                logger.LogInformation("Unauthorized of client certificate.");
                throw new LunaUnauthorizedUserException("Unauthorized of client certificate.");
            }
            if (clientCert == sysCert)
            {
                logger.LogInformation("Successfully verify client certificate.");
            }

        }
    }
}
