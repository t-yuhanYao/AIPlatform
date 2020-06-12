using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Luna.Clients.Azure.APIM;
using Luna.Clients.Controller;
using Luna.Clients.Exceptions;
using Luna.Clients.Logging;
using Luna.Clients.Models.Controller;
using Luna.Data.Entities;
using Luna.Services.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Luna.API.Controllers.Admin
{
    /// <summary>
    /// API controller for product resource.
    /// </summary>
    // [Authorize]
    [ApiController]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("api")]
    public class APIRoutingController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IDeploymentService _deploymentService;
        private readonly IAPIVersionService _apiVersionService;
        private readonly IAMLWorkspaceService _amlWorkspaceService;
        private readonly IAPISubscriptionService _apiSubscriptionService;
        private readonly ILogger<ProductController> _logger;
        private readonly IUserAPIM _userAPIM;

        /// <summary>
        /// Constructor that uses dependency injection.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public APIRoutingController(IProductService productService, IDeploymentService deploymentService, IAPIVersionService apiVersionService, IAMLWorkspaceService amlWorkspaceService, IAPISubscriptionService apiSubscriptionService,
            ILogger<ProductController> logger,
            IUserAPIM userAPIM)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
            _apiVersionService = apiVersionService ?? throw new ArgumentNullException(nameof(apiVersionService));
            _amlWorkspaceService = amlWorkspaceService ?? throw new ArgumentNullException(nameof(amlWorkspaceService));
            _apiSubscriptionService = apiSubscriptionService ?? throw new ArgumentNullException(nameof(apiSubscriptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userAPIM = userAPIM ?? throw new ArgumentNullException(nameof(userAPIM));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/predict")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Predict(string productName, string deploymentName, [FromQuery(Name = "api-version")] string versionName, [FromBody] PredictRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            var version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return this.Content((await ControllerHelper.Predict(version, workspace, request.input)), "application/json");
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/batchinference")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> BatchInferenceWithDefaultModel(string productName, string deploymentName, [FromQuery(Name = "api-version")] string versionName, [FromBody] BatchInferenceRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.BatchInferenceWithDefaultModel(product, deployment, version, workspace, request.subscriptionId, request.userId, request.input));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/train")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> TrainModel(string productName, string deploymentName, [FromQuery(Name = "api-version")] string versionName, [FromBody] TrainModelRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.TrainModel(product, deployment, version, workspace, request.subscriptionId, request.userId, request.input));
        }


        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/operations/training/{modelId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAllTrainingOperationsByModelIdAndVerifyUser(string productName, string deploymentName, Guid subscriptionId, string modelId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetAllTrainingOperationsByModelIdAndVerifyUser(product, deployment, version, workspace, subscriptionId, userId, modelId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/operations/training")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ListAllTrainingOperationsByAUser(string productName, string deploymentName, Guid subscriptionId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.ListAllTrainingOperationsByAUser(product, deployment, version, workspace, subscriptionId, userId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/models/{modelId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAModelByModelIdUserProductDeployment(string productName, string deploymentName, Guid subscriptionId, string modelId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetAModelByModelIdUserProductDeployment(product, deployment, version, workspace, subscriptionId, userId, modelId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/models")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAllModelsByUserProductDeployment(string productName, string deploymentName, Guid subscriptionId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetAllModelsByUserProductDeployment(product, deployment, version, workspace, subscriptionId, userId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/models/{modelId}/batchinference")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> BatchInference(string productName, string deploymentName, string modelId, [FromQuery(Name = "api-version")] string versionName, [FromBody] BatchInferenceRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.BatchInference(product, deployment, version, workspace, request.subscriptionId, request.userId, modelId, request.input));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/operations/inference/{operationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetABatchInferenceOperation(string productName, string deploymentName, string operationId, [FromQuery(Name = "api-version")] string versionName, [FromBody] BatchInferenceRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetABatchInferenceOperation(product, deployment, version, workspace, request.subscriptionId, request.userId, operationId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/operations/inference")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ListAllInferenceOperationsByUser(string productName, string deploymentName, [FromQuery(Name = "api-version")] string versionName, [FromBody] BatchInferenceRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.ListAllInferenceOperationsByUser(product, deployment, version, workspace, request.subscriptionId, request.userId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/models/{modelId}/deploy")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> DeployRealTimePredictionEndpoint(string productName, string deploymentName, string modelId, [FromQuery(Name = "api-version")] string versionName, [FromBody] BatchInferenceRequest request)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(request.subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (request.userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.DeployRealTimePredictionEndpoint(product, deployment, version, workspace, request.subscriptionId, request.userId, modelId, request.input));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/operations/deployment/{endpointId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAllDeployOperationsByEndpointIdAndVerifyUser(string productName, string deploymentName, Guid subscriptionId, string endpointId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetAllDeployOperationsByEndpointIdAndVerifyUser(product, deployment, version, workspace, subscriptionId, userId, endpointId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpPost("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/operations/deployment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ListAllDeployOperationsByUser(string productName, string deploymentName, Guid subscriptionId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.ListAllDeployOperationsByUser(product, deployment, version, workspace, subscriptionId, userId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/endpoints")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAllRealTimeServiceEndpointsByUserProductAndDeployment(string productName, string deploymentName, Guid subscriptionId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetAllRealTimeServiceEndpointsByUserProductAndDeployment(product, deployment, version, workspace, subscriptionId, userId));
        }

        /// <summary>
        /// Gets all apiVersions within a deployment within an product.
        /// </summary>
        /// <param name="productName">The name of the product.</param>
        /// <param name="deploymentName">The name of the deployment.</param>
        /// <returns>HTTP 200 OK with apiVersion JSON objects in response body.</returns>
        [HttpGet("products/{productName}/deployments/{deploymentName}/subscriptions/{subscriptionId}/endpoints/{endpointId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetARealTimeServiceEndpointByEndpointIdUserProductAndDeployment(string productName, string deploymentName, Guid subscriptionId, string endpointId, [FromQuery(Name = "userid")] string userId, [FromQuery(Name = "api-version")] string versionName)
        {
            var apiSubcription = await _apiSubscriptionService.GetAsync(subscriptionId);
            if (apiSubcription == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(apiSubcription)), UserErrorCode.PayloadNotProvided);
            }
            if (userId != _userAPIM.GetUserName(apiSubcription.UserId))
                throw new LunaBadRequestUserException("UserId of request is not equal to apiSubscription.", UserErrorCode.InvalidParameter);

            Product product = new Product();
            Deployment deployment = new Deployment();
            APIVersion version = new APIVersion();
            List<Thread> workerThreads = new List<Thread>();
            Thread productThread = new Thread(async () => {
                product = await _productService.GetAsync(productName);
            });
            workerThreads.Add(productThread);
            productThread.Start();

            Thread deploymentThread = new Thread(async () => {
                deployment = await _deploymentService.GetAsync(productName, deploymentName);
            });
            workerThreads.Add(deploymentThread);
            deploymentThread.Start();

            Thread versionThread = new Thread(async () => {
                version = await _apiVersionService.GetAsync(productName, deploymentName, versionName);
            });
            workerThreads.Add(versionThread);
            versionThread.Start();
            foreach (Thread thread in workerThreads) thread.Join();

            var workspace = await _amlWorkspaceService.GetAsync(version.AMLWorkspaceName);

            return Ok(await ControllerHelper.GetARealTimeServiceEndpointByEndpointIdUserProductAndDeployment(product, deployment, version, workspace, subscriptionId, userId, endpointId));
        }
    }
}