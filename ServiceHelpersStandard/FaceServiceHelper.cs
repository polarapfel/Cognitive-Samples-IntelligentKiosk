// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using Microsoft.ProjectOxford.Common;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceHelpersStandard
{
    public class FaceServiceHelper
    {
        public static int RetryCountOnQuotaLimitError = 6;
        public static int RetryDelayOnQuotaLimitError = 500;

        private static FaceServiceClient faceClient { get; set; }

        public static Action Throttled;

        private static string apiKey;
        public static string ApiKey
        {
            get { return apiKey; }
            set
            {
                var changed = apiKey != value;
                apiKey = value;
                if (changed)
                {
                    InitializeFaceServiceClient();
                }
            }
        }

        private static string apiKeyRegion;
        public static string ApiKeyRegion
        {
            get { return apiKeyRegion; }
            set
            {
                var changed = apiKeyRegion != value;
                apiKeyRegion = value;
                if (changed)
                {
                    InitializeFaceServiceClient();
                }
            }
        }

        private static string apiKeyCustomRegion;
        public static string ApiKeyCustomRegion
        {
            get { return apiKeyCustomRegion; }
            set
            {
                var changed = apiKeyCustomRegion != value;
                apiKeyCustomRegion = value;
                if (changed)
                {
                    InitializeFaceServiceClient();
                }
            }
        }

        private static string apiKeyCustomProtocol;
        public static string ApiKeyCustomProtocol
        {
            get { return apiKeyCustomProtocol; }
            set
            {
                var changed = apiKeyCustomProtocol != value;
                apiKeyCustomProtocol = value;
                if (changed)
                {
                    InitializeFaceServiceClient();
                }
            }
        }

        static FaceServiceHelper()
        {
            InitializeFaceServiceClient();
        }

        private static void InitializeFaceServiceClient()
        {
            switch (ApiKeyRegion)
            {
                case "custom":
                    faceClient = ApiKeyCustomProtocol != null ?
                        new FaceServiceClient(ApiKey, string.Format("{0}://{1}/face/v1.0", ApiKeyCustomProtocol, ApiKeyCustomRegion)) :
                        new FaceServiceClient(ApiKey, string.Format("http://{0}/face/v1.0", ApiKeyCustomRegion)); // without a protocol specified we default to http
                    break;

                default:
                    faceClient = ApiKeyRegion != null ?
                        new FaceServiceClient(ApiKey, string.Format("https://{0}.api.cognitive.microsoft.com/face/v1.0", ApiKeyRegion)) :
                        new FaceServiceClient(ApiKey);
                    break;
            }
        }

        private static async Task<TResponse> RunTaskWithAutoRetryOnQuotaLimitExceededError<TResponse>(Func<Task<TResponse>> action)
        {
            int retriesLeft = FaceServiceHelper.RetryCountOnQuotaLimitError;
            int delay = FaceServiceHelper.RetryDelayOnQuotaLimitError;

            TResponse response = default(TResponse);

            while (true)
            {
                try
                {
                    response = await action();
                    break;
                }
                catch (FaceAPIException exception) when (exception.HttpStatus == (System.Net.HttpStatusCode)429 && retriesLeft > 0)
                {
                    ErrorTrackingHelper.TrackException(exception, "Face API throttling error");
                    if (retriesLeft == 1 && Throttled != null)
                    {
                        Throttled();
                    }

                    await Task.Delay(delay);
                    retriesLeft--;
                    delay *= 2;
                    continue;
                }
            }

            return response;
        }

        private static async Task RunTaskWithAutoRetryOnQuotaLimitExceededError(Func<Task> action)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError<object>(async () => { await action(); return null; });
        }

        public static async Task CreateLargePersonGroupAsync(string personGroupId, string name, string userData)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.CreateLargePersonGroupAsync(personGroupId, name, userData));
        }

        public static async Task<Person[]> GetPersonsAsync(string personGroupId)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<Person[]>(() => faceClient.ListPersonsInLargePersonGroupAsync(personGroupId));
        }

        public static async Task<Face[]> DetectAsync(Func<Task<Stream>> imageStreamCallback, bool returnFaceId = true, bool returnFaceLandmarks = false, IEnumerable<FaceAttributeType> returnFaceAttributes = null)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<Face[]>(async () => await faceClient.DetectAsync(await imageStreamCallback(), returnFaceId, returnFaceLandmarks, returnFaceAttributes));
        }

        public static async Task<Face[]> DetectAsync(string url, bool returnFaceId = true, bool returnFaceLandmarks = false, IEnumerable<FaceAttributeType> returnFaceAttributes = null)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<Face[]>(() => faceClient.DetectAsync(url, returnFaceId, returnFaceLandmarks, returnFaceAttributes));
        }

        public static async Task<PersistedFace> GetPersonFaceAsync(string personGroupId, Guid personId, Guid face)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<PersistedFace>(() => faceClient.GetPersonFaceInLargePersonGroupAsync(personGroupId, personId, face));
        }

        public static async Task<IEnumerable<LargePersonGroup>> ListLargePersonGroupsAsync(string userDataFilter = null)
        {
            return (await RunTaskWithAutoRetryOnQuotaLimitExceededError<LargePersonGroup[]>(() => faceClient.ListLargePersonGroupsAsync())).Where(group => string.IsNullOrEmpty(userDataFilter) || string.Equals(group.UserData, userDataFilter));
        }

        //public static async Task<IEnumerable<LargePersonGroup>> ListPersonGroupsAsync(string userDataFilter = null)
        //{
        //    return (await RunTaskWithAutoRetryOnQuotaLimitExceededError<LargePersonGroup[]>(() => faceClient.ListLargePersonGroupsAsync())).Where(group => string.Equals(group.UserData, userDataFilter));
        //}

        public static async Task<IEnumerable<FaceListMetadata>> GetFaceListsAsync(string userDataFilter = null)
        {
            return (await RunTaskWithAutoRetryOnQuotaLimitExceededError<FaceListMetadata[]>(() => faceClient.ListFaceListsAsync())).Where(list => string.IsNullOrEmpty(userDataFilter) || string.Equals(list.UserData, userDataFilter));
        }

        public static async Task<SimilarPersistedFace[]> FindSimilarAsync(Guid faceId, string faceListId, int maxNumOfCandidatesReturned = 1)
        {
            return (await RunTaskWithAutoRetryOnQuotaLimitExceededError<SimilarPersistedFace[]>(() => faceClient.FindSimilarAsync(faceId, null, faceListId, FindSimilarMatchMode.matchPerson, maxNumOfCandidatesReturned)));
        }

        public static async Task<AddPersistedFaceResult> AddFaceToFaceListAsync(string faceListId, Func<Task<Stream>> imageStreamCallback, FaceRectangle targetFace)
        {
            return (await RunTaskWithAutoRetryOnQuotaLimitExceededError<AddPersistedFaceResult>(async () => await faceClient.AddFaceToLargeFaceListAsync(faceListId, await imageStreamCallback(), null, targetFace)));
        }

        public static async Task CreateLargeFaceListAsync(string faceListId, string name, string userData)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.CreateLargeFaceListAsync(faceListId, name, userData));
        }

        public static async Task DeleteLargeFaceListAsync(string faceListId)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.DeleteLargeFaceListAsync(faceListId));
        }

        public static async Task UpdateLargePersonGroupsAsync(string personGroupId, string name, string userData)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.UpdateLargePersonGroupAsync(personGroupId, name, userData));
        }

        public static async Task<AddPersistedFaceResult> AddPersonFaceAsync(string personGroupId, Guid personId, string imageUrl, string userData, FaceRectangle targetFace)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<AddPersistedFaceResult>(() => faceClient.AddPersonFaceInLargePersonGroupAsync(personGroupId, personId, imageUrl, userData, targetFace));
        }

        public static async Task<AddPersistedFaceResult> AddPersonFaceAsync(string personGroupId, Guid personId, Func<Task<Stream>> imageStreamCallback, string userData, FaceRectangle targetFace)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<AddPersistedFaceResult>(async () => await faceClient.AddPersonFaceInLargePersonGroupAsync(personGroupId, personId, await imageStreamCallback(), userData, targetFace));
        }

        public static async Task<IdentifyResult[]> IdentifyAsync(string personGroupId, Guid[] detectedFaceIds)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<IdentifyResult[]>(() => faceClient.IdentifyAsync(detectedFaceIds, null, personGroupId, 0.5F, 1));
        }

        public static async Task DeletePersonAsync(string personGroupId, Guid personId)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.DeletePersonFromLargePersonGroupAsync(personGroupId, personId));
        }

        public static async Task<CreatePersonResult> CreatePersonAsync(string personGroupId, string name)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<CreatePersonResult>(() => faceClient.CreatePersonInLargePersonGroupAsync(personGroupId, name));
        }

        public static async Task<Person> GetPersonAsync(string personGroupId, Guid personId)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<Person>(() => faceClient.GetPersonInLargePersonGroupAsync(personGroupId, personId));
        }

        public static async Task TrainPersonGroupAsync(string personGroupId)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.TrainLargePersonGroupAsync(personGroupId));
        }

        public static async Task DeletePersonGroupAsync(string personGroupId)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.DeleteLargePersonGroupAsync(personGroupId));
        }

        public static async Task DeletePersonFaceAsync(string personGroupId, Guid personId, Guid faceId)
        {
            await RunTaskWithAutoRetryOnQuotaLimitExceededError(() => faceClient.DeletePersonFaceFromLargePersonGroupAsync(personGroupId, personId, faceId));
        }

        public static async Task<TrainingStatus> GetPersonGroupTrainingStatusAsync(string personGroupId)
        {
            return await RunTaskWithAutoRetryOnQuotaLimitExceededError<TrainingStatus>(() => faceClient.GetLargePersonGroupTrainingStatusAsync(personGroupId));
        }
    }
}
