using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    public sealed class UnityServicesGateway : IUnityServicesGateway
    {
        const string LogPrefix = "[ServicesGateway]";

        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => IsInitialized && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsInitialized ? AuthenticationService.Instance.PlayerId : null;

        public async Task<Result<Unit>> InitializeAndSignInAsync(string profileOverride = null)
        {
            try
            {
                var options = new InitializationOptions();
                if (!string.IsNullOrEmpty(profileOverride))
                    options.SetProfile(profileOverride);

                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(options);

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                Debug.Log($"{LogPrefix} Initialized, PlayerId: {PlayerId}");
                return Result<Unit>.Success(Unit.Value);
            }
            catch (AuthenticationException e)
            {
                Debug.LogError($"{LogPrefix} Auth failed: {e.Message}");
                return Result<Unit>.Failure(OperationErrorCode.AuthenticationFailed, e.Message);
            }
            catch (RequestFailedException e)
            {
                Debug.LogError($"{LogPrefix} Service request failed: {e.Message}");
                return Result<Unit>.Failure(OperationErrorCode.Unexpected, e.Message);
            }
        }
    }
}
