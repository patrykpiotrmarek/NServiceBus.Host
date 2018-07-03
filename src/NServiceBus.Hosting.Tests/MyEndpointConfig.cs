namespace EndpointTypeDeterminerTests
{
    using NServiceBus_6;

    //referenced from in app.config
    class MyEndpointConfig : IConfigureThisEndpoint
    {
        public void Customize(EndpointConfiguration configuration)
        {

        }
    }
}