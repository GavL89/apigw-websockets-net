using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace App
{
    public class Echo
    {
       IAmazonDynamoDB ddbClient = new AmazonDynamoDBClient();
        public async Task<APIGatewayProxyResponse>  FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            LambdaLogger.Log(JObject.FromObject(request).ToString());

            try
            {
                var domainName = request.RequestContext.DomainName;
                var stage = request.RequestContext.Stage;
                var endpoint = $"https://{domainName}/{stage}";
                LambdaLogger.Log("API Gateway management endpoint:" + endpoint);
                
                var message = JsonConvert.DeserializeObject<JObject>(request.Body);
                var data = message["data"]?.ToString();

                var stream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(data));
                
                var scanRequest = new ScanRequest
                {
                    TableName = Environment.GetEnvironmentVariable("DynamoChatTable"),
                    ProjectionExpression = "ConnectionId"
                };
    
                var scanResponse = await ddbClient.ScanAsync(scanRequest);

                var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
                {
                    ServiceURL = endpoint
                });
                
                var count = 0;
                foreach (var item in scanResponse.Items)
                {
                    var connectionId = item["ConnectionId"].S;


                    var postConnectionRequest = new PostToConnectionRequest
                    {
                        ConnectionId = connectionId,
                        Data = stream
                    };

                    try
                    {
                        context.Logger.LogLine($"Post to connection {count}: {connectionId}");
                        stream.Position = 0;
                        await apiClient.PostToConnectionAsync(postConnectionRequest);
                        count++;
                    }
                    catch (AmazonServiceException e)
                    {
                        // API Gateway returns a status of 410 GONE when the connection is no
                        // longer available. If this happens, we simply delete the identifier
                        // from our DynamoDB table.
                        if (e.StatusCode == HttpStatusCode.Gone)
                        {
                            var ddbDeleteRequest = new DeleteItemRequest
                            {
                                TableName = Environment.GetEnvironmentVariable("DynamoChatTable"),
                                Key = new Dictionary<string, AttributeValue>
                                {
                                    {"ConnectionId", new AttributeValue {S = connectionId}}
                                }
                            };

                            context.Logger.LogLine($"Deleting gone connection: {connectionId}");
                            await ddbClient.DeleteItemAsync(ddbDeleteRequest);
                        }
                        else
                        {
                            context.Logger.LogLine($"Error posting message to {connectionId}: {e.Message}");
                            context.Logger.LogLine(e.StackTrace);                            
                        }
                    }
                }

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Data send to " + count + " connection" + (count == 1 ? "" : "s")
                };
            }
            catch (Exception e)
            {
                LambdaLogger.Log("Error disconnecting: " + e.Message);
                LambdaLogger.Log(e.StackTrace);
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed to send message: {e.Message}" 
                };
            }
        }
    }
}