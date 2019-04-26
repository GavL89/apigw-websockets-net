using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace App
{
    public class Disconnect
    {
        IAmazonDynamoDB ddbClient = new AmazonDynamoDBClient();
        public async Task<APIGatewayProxyResponse>  FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            LambdaLogger.Log(JObject.FromObject(request).ToString());
            try {
                var connectionId = request.RequestContext.ConnectionId;
                LambdaLogger.Log("ConnectionId:" + connectionId);

                Dictionary<string, AttributeValue> attributes = new Dictionary<string, AttributeValue>();
                attributes["ConnectionId"] = new AttributeValue { S = connectionId };

                DeleteItemRequest ddbRequest = new DeleteItemRequest() {
                    TableName = Environment.GetEnvironmentVariable("DynamoChatTable"),
                    Key = attributes
                };
                DeleteItemResponse ddbResponse = ddbClient.DeleteItemAsync(ddbRequest).Result;

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Disconnected."
                };
            } catch (Exception e) {
                context.Logger.LogLine("Error connecting: " + e.Message);
                context.Logger.LogLine(e.StackTrace);
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed to connect: {e.Message}" 
                };
            }
        }
    }
}