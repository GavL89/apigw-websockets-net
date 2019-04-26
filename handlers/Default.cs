using Amazon.Lambda.Core;
using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace App
{
    public class Default
    {
       public JObject FunctionHandler(JObject input)
       {
           LambdaLogger.Log(input.ToString());
           return input;
       }
    }
}