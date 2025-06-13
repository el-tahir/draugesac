using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;

namespace Draugesac.Infra
{
    internal sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            // Ensure the stack name "DraugesacInfraStack" is what you want to appear in CloudFormation
            new DraugesacInfraStack(app, "DraugesacInfraStack", new StackProps
            {
                // If you don't specify 'env', this stack will be environment-agnostic.
                // Account/Region-dependent features and context lookups will not work,
                // but a single synthesized template can be deployed anywhere.
                // For S3 bucket names that need to be globally unique, it's better to make them region/account specific or add a unique suffix.
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
                }
            });
            app.Synth();
        }
    }
}
