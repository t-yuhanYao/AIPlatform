# Test and publish Azure Machine Learning pipelines

In this article, we will show you how to test your code and publish it into Azure Machine Learning pipeline. Later you will be able to use the published pipeline endpoint to create your AI service.

You need to run this if you want to pubish a AI service which allows user to train models using their own data, do batch inference using the models and/or deploy models to service endpoints for real-time scoring. If you only want to publish a pre-trained model as a AI service, you can skip this article.

## Prepare test data

Before running the test, you need to prepare your test data for training, batch inference and real-time scoring. The test data will simulate the end user input when they are calling the AI service which we will be publishing later in this tutorial.

### Create a storage container for batch inference result and get Shared Access Signiture (SaS key)

When we [add your code to the ML project](./add-ml-code.md), we uploaded the batch inference result to the Azure storage as a blob file. In this example, you will need to create a container in your storage account and get a Shared Access Signiture to be able to write files in the container. You can do this either in Azure Storage Explorer or run the following PowerShell commands:

```powershell
$resourceGroupName = 'my-resource-group'
$storageAccountName = 'my-storage-account'
$containerName = "my-container"

Connect-AzAccount

$storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroupName -Name $storageAccountName

$ctx = $storageAccount.Context

$container = New-AzStorageContainer -Name $containerName -Context $ctx

$token = New-AzStorageContainerSASToken -Name $containerName -Context $ctx -Permission rwdl

Write-Host $token
```

You can also create the container and get the SaS key in Azure Storage Explorer. See [here](https://docs.microsoft.com/en-us/azure/vs-azure-tools-storage-manage-with-storage-explorer?tabs=windows#account-level-shared-access-signature) for more details.

### Update test data in test_data.json

In the Luna project template, the test data is defined in *tests/azureml/test_data.json*. You need to define 3 inputs and 1 output data:

- training_user_input: it is the input data for the training API call. An example of the training input data for our Logistic Regression model is:

  ```json
  {
      "trainingDataSource": "https://xiwutestai.blob.core.windows.net/lunav2/iris/iris.csv?st=2020-07-22T17%3A19%3A10Z&se=2027-10-12T17%3A19%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=7c%2BaoI8QtdepDHKqJqjjljdBUyDyuL8wbKol2Kn7xaI%3D",
      "description": "Iris classification model"
  }
  ```

- batch_inference_input: it is the input data for the batch inference API call. An example of the batch inference input data will be:

  ```json
  {
      "dataSource": "https://xiwutestai.blob.core.windows.net/lunav2/iris/iris_test.csv?st=2020-07-22T20%3A52%3A09Z&se=2031-10-14T20%3A52%3A00Z&sp=rl&sv=2018-03-28&sr=b&sig=Thjj%2BjB4GSvWMIUuqKJLLhYLfJSq4uhf%2B7A5ai6qSoA%3D",
      "output": "https://<your-storage-account-name>.blob.core.windows.net/<your-container-name>/iris/result.csv<your-sas-key>"
  }
  ```

- real_time_scoring_input: it is the input data for the real time scoring. An example of the real time scoring input data for our Iris classification model will be:

  ```json
  {
      "records":[[5.1,3.5,1.4,0.2]]
  }
  ```

- real_time_scoring_expected_output: it is the expected output of the real time scoring call. We will be comparing it to the real output. An example of the expected output data from the input above will be:

  ```json
  {
      "result":[1]
  }
  ```

## Run the test

The test script is located at *tests/azureml/azureml_test.py*. Run the following command in the Anaconda console from the root folder of the project

```shell
python tests/azureml/azureml_test.py
```

If you are using a different test data file, you can specify the test data file path by the *--test_data_file_path* parameter:

```shell
python tests/azureml/azureml_test.py --test_data_file_path <test-data-file-path>
```

The test will:

- Schedule an AML pipeline run to train a model and regiester the model to AML workspace
- Poll the model training run status by model id and wait until it completed
- Schedule an AML pipeline run to do batch inference using the model we just trained
- Poll the batch inference run status by the operation id and wait until it completed
- Schedule an AML pipeline run to deploy the model to a service endpoint for real-time scoring
- Poll the deploymenet run status by the endpoint id and wait until it completed
- Test the real-time endpoint and compare the result with expected result

## Troubleshoot issues

When we start a pipeline run, we will print out the *"Link to Azure Machine Learning Portal"* in the ternimal. If your pipeline run failed, you can click into the link and find out the detailed errors. Fix those errors and schedule the run again.

![aml-workspace-failed-run](../../images/luna.ai/aml-workspace-failed-run.png)

## Publish code to AML pipelines

If all local tests passed, we are ready to publish the code to AML pipelines. The script to publish the pipelines is located at *src/luna_publish/azureml/publish_azureml_pipelines.py*.

Before running the script, you should update the pipeline names to the values you want. In this example, we will use the following values:

Run the following command in the Anaconda console from the root folder of the project

```shell
python tests/azureml/src/luna_publish/azureml/publish_azureml_pipelines.py
```

You can specify the following parameters:

- *--training_pipeline_name*: the name of training pipeline, default value is *mytrainingpipeline*
- *--batch_inference_pipeline_name*: the name of batch inference pipeline, default value is *mybatchinferencepipeline*
- *--deployment_pipeline_name*: the name of deployment pipeline, default value is *deployment_pipeline_name*
- *--aml_workspace_name*: the name of the AML workspace, if not specified, the one defined in the test_workspace.json will be used
- *--subscription_id*: the subscription id of the AML workspace, if not specified, the one defined in the test_workspace.json will be used
- *--resource_group*: the resource group name of the AML workspace, if not specified, the one defined in the test_workspace.json will be used

It will publish all 3 pipelines to AML workspace:

- training
- batch inference
- deployment

Save the output. You will need to use it in the next step.

## Next Step

[Publish an AI service](./publish-ai-service.md)
