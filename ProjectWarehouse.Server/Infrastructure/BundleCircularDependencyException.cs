namespace ProjectWarehouse.Server.Infrastructure;

public class BundleCircularDependencyException()
    : Exception("Circular dependency detected in bundle components."), IExpectedFailure;
