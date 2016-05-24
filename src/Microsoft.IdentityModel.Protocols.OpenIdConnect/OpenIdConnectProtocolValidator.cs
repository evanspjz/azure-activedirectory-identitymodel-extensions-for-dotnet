//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.IdentityModel.Protocols.OpenIdConnect
{
    /// <summary>
    /// Delegate for validating additional claims in 'id_token' 
    /// </summary>
    /// <param name="idToken"><see cref="JwtSecurityToken"/> to validate</param>
    /// <param name="context"><see cref="OpenIdConnectProtocolValidationContext"/> used for validation</param>
    public delegate void IdTokenValidator(JwtSecurityToken idToken, OpenIdConnectProtocolValidationContext context);

    /// <summary>
    /// <see cref="OpenIdConnectProtocolValidator"/> is used to ensure that an <see cref="OpenIdConnectMessage"/>
    ///  obtained using OpenIdConnect is compliant with  http://openid.net/specs/openid-connect-core-1_0.html .
    /// </summary>
    public class OpenIdConnectProtocolValidator
    {
        private IDictionary<string, string> _hashAlgorithmMap =
            new Dictionary<string, string>
            {
                { SecurityAlgorithms.EcdsaSha256, "SHA256" },
                { SecurityAlgorithms.RsaSha256, "SHA256" },
                { SecurityAlgorithms.HmacSha256, "SHA256" },
                { SecurityAlgorithms.EcdsaSha384, "SHA384" },
                { SecurityAlgorithms.RsaSha384, "SHA384" },
                { SecurityAlgorithms.HmacSha384, "SHA384" },
                { SecurityAlgorithms.EcdsaSha512, "SHA512" },
                { SecurityAlgorithms.RsaSha512, "SHA512" },
                { SecurityAlgorithms.HmacSha512, "SHA512" },
          };

        private TimeSpan _nonceLifetime = DefaultNonceLifetime;

        /// <summary>
        /// Default for the how long the nonce is valid.
        /// </summary>
        /// <remarks>default: 1 hour.</remarks>
        public static readonly TimeSpan DefaultNonceLifetime = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Creates a new instance of <see cref="OpenIdConnectProtocolValidator"/>,
        /// </summary>
        public OpenIdConnectProtocolValidator()
        {
            RequireAcr = false;
            RequireAmr = false;
            RequireAuthTime = false;
            RequireAzp = false;
            RequireNonce = true;
            RequireState = true;
            RequireSub = false;
            RequireTimeStampInNonce = true;
            RequireStateValidation = true;
        }

        /// <summary>
        /// Generates a value suitable to use as a nonce.
        /// </summary>
        /// <returns>a nonce</returns>
        /// <remarks>if <see cref="RequireTimeStampInNonce"/> is true then the 'nonce' will contain the Epoch time as the prefix, seperated by a '.'.
        /// <para>for example: 635410359229176103.MjQxMzU0ODUtMTdiNi00NzAwLWE4MjYtNTE4NGExYmMxNTNlZmRkOGU4NjctZjQ5OS00MWIyLTljNTEtMjg3NmM0NzI4ZTc5</para></remarks>
        public virtual string GenerateNonce()
        {
            IdentityModelEventSource.Logger.WriteVerbose(LogMessages.IDX10328);
            string nonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + Guid.NewGuid().ToString()));
            if (RequireTimeStampInNonce)
            {
                return DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) + "." + nonce;
            }

            return nonce;
        }

        /// <summary>
        /// Gets the algorithm mapping between OpenIdConnect and .Net for Hash algorithms.
        /// a <see cref="IDictionary{TKey, TValue}"/> that contains mappings from the JWT namespace http://tools.ietf.org/html/rfc7518 to .Net.
        /// </summary>
        public IDictionary<string, string> HashAlgorithmMap
        {
            get
            {
                return _hashAlgorithmMap;
            }
        }

        /// <summary>
        /// Gets or set the <see cref="TimeSpan"/> defining how long a nonce is valid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if 'value' is less than or equal to 'TimeSpan.Zero'.</exception>
        /// <remarks>if <see cref="RequireTimeStampInNonce"/> is true, then the nonce timestamp is bound by DateTime.UtcNow + NonceLifetime.</remarks>
        public TimeSpan NonceLifetime
        {
            get
            {
                return _nonceLifetime;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw LogHelper.LogArgumentException<ArgumentOutOfRangeException>("value", LogMessages.IDX10105, value);
                }

                _nonceLifetime = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if an 'acr' claim is required.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireAcr { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if an 'amr' claim is required.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireAmr { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if an 'auth_time' claim is required.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireAuthTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if an 'azp' claim is required.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireAzp { get; set; }

        /// <summary>
        /// Get or sets if a nonce is required.
        /// </summary>
        [DefaultValue(true)]
        public bool RequireNonce { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if a 'state' is required.
        /// </summary>
        [DefaultValue(true)]
        public bool RequireState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if validation of 'state' is turned on or off.
        /// </summary>
        [DefaultValue(true)]
        public bool RequireStateValidation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if a 'sub' claim is required.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireSub { get; set; }

        /// <summary>
        /// Gets or set logic to control if a nonce is prefixed with a timestamp.
        /// </summary>
        /// <remarks>if <see cref="RequireTimeStampInNonce"/> is true then:
        /// <para><see cref="GenerateNonce"/> will return a 'nonce' with the Epoch time as the prefix, delimited with a '.'.</para>
        /// <para><see cref="ValidateNonce"/> will require that the 'nonce' has a valid time as the prefix.</para>
        /// </remarks>
        [DefaultValue(true)]
        public bool RequireTimeStampInNonce { get; set; }

        /// <summary>
        /// Gets or sets the delegate for validating 'id_token'
        /// </summary>
        public IdTokenValidator IdTokenValidator { get; set; }

        /// <summary>
        /// Validates that an OpenIdConnect Response from 'authorization_endpoint" is valid as per http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="validationContext">the <see cref="OpenIdConnectProtocolValidationContext"/> that contains expected values.</param>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolException">if the response is not spec compliant.</exception>
        /// <remarks>It is assumed that the IdToken had ('aud', 'iss', 'signature', 'lifetime') validated.</remarks>
        public virtual void ValidateAuthenticationResponse(OpenIdConnectProtocolValidationContext validationContext)
        {
            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            // no 'response' is recieved or 'id_token' in the response is null 
            if (validationContext.ProtocolMessage == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10333);

            if (string.IsNullOrEmpty(validationContext.ProtocolMessage.IdToken))
            {
                // if 'code' is also not present, then throw.
                if (string.IsNullOrEmpty(validationContext.ProtocolMessage.Code))
                {
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10334);
                }
                else
                {
                    ValidateState(validationContext);
                }
                return;
            }

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10332);

            // 'refresh_token' should not be returned from 'authorization_endpoint'. http://tools.ietf.org/html/rfc6749#section-4.2.2.
            if (!string.IsNullOrEmpty(validationContext.ProtocolMessage.RefreshToken))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10335);

            ValidateState(validationContext);
            ValidateIdToken(validationContext);
            ValidateNonce(validationContext);
            ValidateCHash(validationContext);
            ValidateAtHash(validationContext);
        }

        /// <summary>
        /// Validates that an OpenIdConnect Response from "token_endpoint" is valid as per http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="validationContext">the <see cref="OpenIdConnectProtocolValidationContext"/> that contains expected values.</param>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolException">if the response is not spec compliant.</exception>
        /// <remarks>It is assumed that the IdToken had ('aud', 'iss', 'signature', 'lifetime') validated.</remarks>
        public virtual void ValidateTokenResponse(OpenIdConnectProtocolValidationContext validationContext)
        {
            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            // no 'response' is recieved 
            if (validationContext.ProtocolMessage == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10333);

            // both 'id_token' and 'access_token' are required
            if (string.IsNullOrEmpty(validationContext.ProtocolMessage.IdToken) || string.IsNullOrEmpty(validationContext.ProtocolMessage.AccessToken))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10336);

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10332);

            ValidateIdToken(validationContext);
            ValidateNonce(validationContext);


            // only if 'at_hash' claim exist. 'at_hash' is not required in token response.
            object atHashClaim;
            if (validationContext.ValidatedIdToken.Payload.TryGetValue(JwtRegisteredClaimNames.AtHash, out atHashClaim))
            {
                ValidateAtHash(validationContext);
            }

        }

        /// <summary>
        /// Validates that an OpenIdConnect Response from "useinfo_endpoint" is valid as per http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="validationContext">the <see cref="OpenIdConnectProtocolValidationContext"/> that contains expected values.</param>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolException">if the response is not spec compliant.</exception>
        public virtual void ValidateUserInfoResponse(OpenIdConnectProtocolValidationContext validationContext)
        {
            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (string.IsNullOrEmpty(validationContext.UserInfoEndpointResponse))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10337);

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10332);

            string sub = string.Empty;
            try
            {
                // if user info response is a jwt token
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(validationContext.UserInfoEndpointResponse))
                {
                    var token = handler.ReadToken(validationContext.UserInfoEndpointResponse) as JwtSecurityToken;
                    sub = token.Payload.Sub;
                }
                else
                {
                    // if the response is not a jwt, it should be json
                    var payload = JwtPayload.Deserialize(validationContext.UserInfoEndpointResponse);
                    sub = payload.Sub;
                }
            }
            catch (Exception ex)
            {
                throw LogHelper.LogException<OpenIdConnectProtocolException>(ex, LogMessages.IDX10343, validationContext.UserInfoEndpointResponse);
            }

            if (string.IsNullOrEmpty(sub))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10345);

            if (string.IsNullOrEmpty(validationContext.ValidatedIdToken.Payload.Sub))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10346);

            if (!string.Equals(validationContext.ValidatedIdToken.Payload.Sub, sub, StringComparison.Ordinal))
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10338, validationContext.ValidatedIdToken.Payload.Sub, sub);
        }

        /// <summary>
        /// Validates the claims in the 'id_token' as per http://openid.net/specs/openid-connect-core-1_0.html#IDTokenValidation
        /// </summary>
        /// <param name="validationContext">the <see cref="OpenIdConnectProtocolValidationContext"/> that contains expected values.</param>
        protected virtual void ValidateIdToken(OpenIdConnectProtocolValidationContext validationContext)
        {
            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogArgumentNullException("validationContext.ValidatedIdToken");

            // if user sets the custom validator, we call the delegate. The default checks for multiple audiences and azp are not executed.
            if (this.IdTokenValidator != null)
            {
                try
                {
                    this.IdTokenValidator(validationContext.ValidatedIdToken, validationContext);
                }
                catch (Exception ex)
                {
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(ex, LogMessages.IDX10313, validationContext.ValidatedIdToken);
                }
                return;
            }
            else
            {
                JwtSecurityToken idToken = validationContext.ValidatedIdToken;

                // required claims
                if (idToken.Payload.Aud.Count == 0)
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10314, JwtRegisteredClaimNames.Aud.ToLowerInvariant(), idToken);

                if (!idToken.Payload.Exp.HasValue)
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10314, JwtRegisteredClaimNames.Exp.ToLowerInvariant(), idToken);

                if (!idToken.Payload.Iat.HasValue)
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10314, JwtRegisteredClaimNames.Iat.ToLowerInvariant(), idToken);

                if (idToken.Payload.Iss == null)
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10314, JwtRegisteredClaimNames.Iss.ToLowerInvariant(), idToken);

                // sub is optional by default
                if (RequireSub && (string.IsNullOrWhiteSpace(idToken.Payload.Sub)))
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10314, JwtRegisteredClaimNames.Sub.ToLowerInvariant(), idToken);

                // optional claims
                if (RequireAcr && string.IsNullOrWhiteSpace(idToken.Payload.Acr))
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10315, idToken);

                if (RequireAmr && idToken.Payload.Amr.Count == 0)
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10316, idToken);

                if (RequireAuthTime && !(idToken.Payload.AuthTime.HasValue))
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10317, idToken);

                if (RequireAzp && string.IsNullOrWhiteSpace(idToken.Payload.Azp))
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10318, idToken);

                // if multiple audiences are present in the id_token, 'azp' claim should be present
                if (idToken.Payload.Aud.Count > 1 && string.IsNullOrEmpty(idToken.Payload.Azp))
                {
                    IdentityModelEventSource.Logger.WriteWarning(LogMessages.IDX10339);
                }

                // if 'azp' claim exist, it should be equal to 'client_id' of the application
                if (!string.IsNullOrEmpty(idToken.Payload.Azp))
                {
                    if (string.IsNullOrEmpty(validationContext.ClientId))
                    {
                        throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10308);
                    }
                    else if (!string.Equals(idToken.Payload.Azp, validationContext.ClientId, StringComparison.Ordinal))
                    {
                        throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10340, idToken.Payload.Azp, validationContext.ClientId);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the implementation corresponding to the input string 'algorithm'.
        /// </summary>
        /// <param name="algorithm">string representing the hash algorithm</param>
        /// <returns><see cref="HashAlgorithm"/> corresponding to the input string 'algorithm'</returns>
        public virtual HashAlgorithm GetHashAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                algorithm = SecurityAlgorithms.RsaSha256;
            }

            try
            {
                switch (algorithm)
                {
                    case SecurityAlgorithms.Sha256:
                    case SecurityAlgorithms.EcdsaSha256:
                    case SecurityAlgorithms.HmacSha256:
                    case SecurityAlgorithms.RsaSha256:
                    case SecurityAlgorithms.RsaSsaPssSha256:
                    case SecurityAlgorithms.RsaSha256Signature:
                    case SecurityAlgorithms.EcdsaSha256Signature:
                        return SHA256.Create();

                    case SecurityAlgorithms.Sha384:
                    case SecurityAlgorithms.EcdsaSha384:
                    case SecurityAlgorithms.HmacSha384:
                    case SecurityAlgorithms.RsaSha384:
                    case SecurityAlgorithms.RsaSsaPssSha384:
                    case SecurityAlgorithms.RsaSha384Signature:
                    case SecurityAlgorithms.EcdsaSha384Signature:
                        return SHA384.Create();

                    case SecurityAlgorithms.Sha512:
                    case SecurityAlgorithms.RsaSha512:
                    case SecurityAlgorithms.EcdsaSha512:
                    case SecurityAlgorithms.HmacSha512:
                    case SecurityAlgorithms.RsaSsaPssSha512:
                    case SecurityAlgorithms.RsaSha512Signature:
                    case SecurityAlgorithms.EcdsaSha512Signature:
                        return SHA512.Create();
                }

            }
            catch (Exception ex)
            {
                throw LogHelper.LogException<OpenIdConnectProtocolException>(ex, LogMessages.IDX10301, algorithm);
            }

            throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10302, algorithm);
        }

        /// <summary>
        /// Validates the 'token' or 'code' see: http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="expectedValue">the expected value of the hash. normally the c_hash or at_hash claim.</param>
        /// <param name="hashItem">item to be hashed per oidc spec.</param>
        /// <param name="algorithm">algorithm to use for compute hash.</param>
        /// <exception cref="OpenIdConnectProtocolException">if expected value does not equal the hashed value.</exception>
        private void ValidateHash(string expectedValue, string hashItem, string algorithm)
        {
            IdentityModelEventSource.Logger.WriteInformation(LogMessages.IDX10303, expectedValue);
            using (var hashAlgorithm = GetHashAlgorithm(algorithm))
            {
                var hashBytes = hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(hashItem));
                var hashString = Base64UrlEncoder.Encode(hashBytes, 0, hashBytes.Length / 2);
                if (!string.Equals(expectedValue, hashString, StringComparison.Ordinal))
                {
                    throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10300, expectedValue, hashItem, algorithm);
                }
            }
        }

        /// <summary>
        /// Validates the 'code' according to http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="validationContext">a <see cref="OpenIdConnectProtocolValidationContext"/> that contains the protocol message to validate.</param>
        /// <exception cref="ArgumentNullException">if 'idToken' is null.</exception>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidCHashException">if the validationContext contains a 'code' and there is no 'c_hash' claim in the 'id_token'.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidCHashException">if the validationContext contains a 'code' and the 'c_hash' claim is not a string in the 'id_token'.</exception> 
        /// <exception cref="OpenIdConnectProtocolInvalidCHashException">if the 'c_hash' claim in the 'idToken' does not correspond to the 'code' in the <see cref="OpenIdConnectMessage"/> response.</exception> 
        protected virtual void ValidateCHash(OpenIdConnectProtocolValidationContext validationContext)
        {
            IdentityModelEventSource.Logger.WriteVerbose(LogMessages.IDX10304);

            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogArgumentNullException("validationContext.ValidatedIdToken");

            if (validationContext.ProtocolMessage == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10333);

            if (string.IsNullOrEmpty(validationContext.ProtocolMessage.Code))
            {
                IdentityModelEventSource.Logger.WriteInformation(LogMessages.IDX10305);
                return;
            }

            object cHashClaim;
            if (!validationContext.ValidatedIdToken.Payload.TryGetValue(JwtRegisteredClaimNames.CHash, out cHashClaim))
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidCHashException>(LogMessages.IDX10307, validationContext.ValidatedIdToken);
            }

            var chash = cHashClaim as string;
            if (chash == null)
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidCHashException>(LogMessages.IDX10306, validationContext.ValidatedIdToken);
            }

            try
            {
                ValidateHash(chash, validationContext.ProtocolMessage.Code, validationContext.ValidatedIdToken.Header.Alg);
            }
            catch(OpenIdConnectProtocolException ex)
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidCHashException>(ex, LogMessages.IDX10347);
            }
        }

        /// <summary>
        /// Validates the 'token' according to http://openid.net/specs/openid-connect-core-1_0.html
        /// </summary>
        /// <param name="validationContext">a <see cref="OpenIdConnectProtocolValidationContext"/> that contains the protocol message to validate.</param>
        /// <exception cref="ArgumentNullException">if 'idToken' is null.</exception>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidAtHashException">if the validationContext contains a 'token' and there is no 'at_hash' claim in the id_token.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidAtHashException">if the validationContext contains a 'token' and the 'at_hash' claim is not a string in the 'id_token'.</exception> 
        /// <exception cref="OpenIdConnectProtocolInvalidCHashException">if the 'at_hash' claim in the 'idToken' does not correspond to the 'token' in the <see cref="OpenIdConnectMessage"/> response.</exception> 
        protected virtual void ValidateAtHash(OpenIdConnectProtocolValidationContext validationContext)
        {
            IdentityModelEventSource.Logger.WriteVerbose(LogMessages.IDX10309);

            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogArgumentNullException("validationContext.ValidatedIdToken");

            if (validationContext.ProtocolMessage == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10333);

            if (validationContext.ProtocolMessage.AccessToken == null)
            {
                IdentityModelEventSource.Logger.WriteInformation(LogMessages.IDX10310);
                return;
            }

            object atHashClaim;
            if (!validationContext.ValidatedIdToken.Payload.TryGetValue(JwtRegisteredClaimNames.AtHash, out atHashClaim))
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidAtHashException>(LogMessages.IDX10312, validationContext.ValidatedIdToken);

            var atHash = atHashClaim as string;
            if (atHash == null)
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidAtHashException>(LogMessages.IDX10311, validationContext.ValidatedIdToken);

            try
            {
                ValidateHash(atHash, validationContext.ProtocolMessage.AccessToken, validationContext.ValidatedIdToken.Header.Alg);
            }
            catch (OpenIdConnectProtocolException ex)
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidAtHashException>(ex, LogMessages.IDX10348);
            }
        }

        /// <summary>
        /// Validates that the <see cref="JwtSecurityToken"/> contains the nonce.
        /// </summary>
        /// <param name="validationContext">a <see cref="OpenIdConnectProtocolValidationContext"/> that contains the 'nonce' to validate.</param>
        /// <exception cref="ArgumentNullException">if 'idToken' is null.</exception>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidNonceException">if <see cref="OpenIdConnectProtocolValidationContext.Nonce"/> is null and RequireNonce is true.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidNonceException">if the 'nonce' found in the 'id_token' does not match <see cref="OpenIdConnectProtocolValidationContext.Nonce"/>.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidNonceException">if <see cref="RequireTimeStampInNonce"/> is true and a timestamp is not: found, well formed, negatire or expired.</exception>
        /// <remarks>The timestamp is only validated if <see cref="RequireTimeStampInNonce"/> is true.
        /// <para>If <see cref="OpenIdConnectProtocolValidationContext.Nonce"/> is not-null, then a matching 'nonce' must exist in the 'id_token'.</para></remarks>
        protected virtual void ValidateNonce(OpenIdConnectProtocolValidationContext validationContext)
        {
            IdentityModelEventSource.Logger.WriteVerbose(LogMessages.IDX10319);

            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (validationContext.ValidatedIdToken == null)
                throw LogHelper.LogArgumentNullException("validationContext.ValidatedIdToken");

            string nonceFoundInJwt = validationContext.ValidatedIdToken.Payload.Nonce;
            if (!RequireNonce && string.IsNullOrEmpty(validationContext.Nonce) && string.IsNullOrEmpty(nonceFoundInJwt))
            {
                IdentityModelEventSource.Logger.WriteInformation(LogMessages.IDX10322);
                return;
            }
            else if (string.IsNullOrEmpty(validationContext.Nonce))
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10320, RequireNonce.ToString());

            }
            else if (string.IsNullOrEmpty(nonceFoundInJwt))
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10323, RequireNonce.ToString(), validationContext.ValidatedIdToken);
            }

            if (!string.Equals(nonceFoundInJwt, validationContext.Nonce, StringComparison.Ordinal))
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10321, validationContext.Nonce, nonceFoundInJwt, validationContext.ValidatedIdToken);

            if (RequireTimeStampInNonce)
            {
                int endOfTimestamp = nonceFoundInJwt.IndexOf('.');
                if (endOfTimestamp == -1)
                    throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10325, validationContext.Nonce);

                string timestamp = nonceFoundInJwt.Substring(0, endOfTimestamp);
                DateTime nonceTime = new DateTime(1979, 1, 1);          // initializing to some value otherwise it gives an error
                long ticks = -1;
                try
                {
                    ticks = Convert.ToInt64(timestamp, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(ex, LogMessages.IDX10326, timestamp, validationContext.Nonce);
                }

                if (ticks <= 0)
                    throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10326, timestamp, validationContext.Nonce);

                try
                {
                    nonceTime = DateTime.FromBinary(ticks);
                }
                catch(Exception ex)
                {
                    throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(ex, LogMessages.IDX10327, timestamp, DateTime.MinValue.Ticks.ToString(CultureInfo.InvariantCulture), DateTime.MaxValue.Ticks.ToString(CultureInfo.InvariantCulture));
                }

                DateTime utcNow = DateTime.UtcNow;
                if (nonceTime + NonceLifetime < utcNow)
                    throw LogHelper.LogException<OpenIdConnectProtocolInvalidNonceException>(LogMessages.IDX10324, validationContext.Nonce, nonceTime.ToString(), utcNow.ToString(), NonceLifetime.ToString());
            }
        }

        /// <summary>
        /// Validates that the 'state' in message is valid.
        /// </summary>
        /// <param name="validationContext">a <see cref="OpenIdConnectProtocolValidationContext"/> that contains the 'state' to validate.</param>
        /// <exception cref="ArgumentNullException">if 'validationContext' is null.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidStateException">if 'state' is present in <see cref="OpenIdConnectProtocolValidationContext.State"/> but either <see cref="OpenIdConnectProtocolValidationContext.ProtocolMessage"/> or <see cref="OpenIdConnectProtocolValidationContext.ProtocolMessage.State"/> is null.</exception>
        /// <exception cref="OpenIdConnectProtocolInvalidStateException">if 'state' in the context does not match the state in the message.</exception>
        protected virtual void ValidateState(OpenIdConnectProtocolValidationContext validationContext)
        {
            if (!RequireStateValidation)
            {
                IdentityModelEventSource.Logger.WriteVerbose(LogMessages.IDX10342);
                return;
            }

            if (validationContext == null)
                throw LogHelper.LogArgumentNullException("validationContext");

            if (validationContext.ProtocolMessage == null)
                throw LogHelper.LogException<OpenIdConnectProtocolException>(LogMessages.IDX10333);

            // if state is missing, but not required just return. Otherwise process it.
            if (!RequireState && string.IsNullOrEmpty(validationContext.State) && string.IsNullOrEmpty(validationContext.ProtocolMessage.State))
            {
                IdentityModelEventSource.Logger.WriteInformation(LogMessages.IDX10341);
                return;
            }
            else if (string.IsNullOrEmpty(validationContext.State))
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidStateException>(LogMessages.IDX10329, RequireState);
            }
            else if (string.IsNullOrEmpty(validationContext.ProtocolMessage.State))
            {
                // 'state' was sent, but message does not contain 'state'
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidStateException>(LogMessages.IDX10330, RequireState.ToString());
            }

            if (!string.Equals(validationContext.State, validationContext.ProtocolMessage.State, StringComparison.Ordinal))
            {
                throw LogHelper.LogException<OpenIdConnectProtocolInvalidStateException>(LogMessages.IDX10331, validationContext.State, validationContext.ProtocolMessage.State);
            }
        }
    }
}
