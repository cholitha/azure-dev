<?xml version="1.0" encoding="utf-8"?>
<serviceModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" name="CrawlerWorkerRole" generation="1" functional="0" release="0" Id="7278206e-ba9a-4758-9382-7e1d4c3f3c05" dslVersion="1.2.0.0" xmlns="http://schemas.microsoft.com/dsltools/RDSM">
  <groups>
    <group name="CrawlerWorkerRoleGroup" generation="1" functional="0" release="0">
      <settings>
        <aCS name="WorkerRoleService:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/MapWorkerRoleService:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </maps>
        </aCS>
        <aCS name="WorkerRoleServiceInstances" defaultValue="[1,1,1]">
          <maps>
            <mapMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/MapWorkerRoleServiceInstances" />
          </maps>
        </aCS>
      </settings>
      <maps>
        <map name="MapWorkerRoleService:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/WorkerRoleService/Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </setting>
        </map>
        <map name="MapWorkerRoleServiceInstances" kind="Identity">
          <setting>
            <sCSPolicyIDMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/WorkerRoleServiceInstances" />
          </setting>
        </map>
      </maps>
      <components>
        <groupHascomponents>
          <role name="WorkerRoleService" generation="1" functional="0" release="0" software="C:\Users\ins_t\Documents\AutomationOfficial\spiderautomation\CrawlerWorkerRole\csx\Debug\roles\WorkerRoleService" entryPoint="base\x64\WaHostBootstrapper.exe" parameters="base\x64\WaWorkerHost.exe " memIndex="-1" hostingEnvironment="consoleroleadmin" hostingEnvironmentVersion="2">
            <settings>
              <aCS name="Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="" />
              <aCS name="__ModelData" defaultValue="&lt;m role=&quot;WorkerRoleService&quot; xmlns=&quot;urn:azure:m:v1&quot;&gt;&lt;r name=&quot;WorkerRoleService&quot; /&gt;&lt;/m&gt;" />
            </settings>
            <resourcereferences>
              <resourceReference name="DiagnosticStore" defaultAmount="[4096,4096,4096]" defaultSticky="true" kind="Directory" />
              <resourceReference name="EventStore" defaultAmount="[1000,1000,1000]" defaultSticky="false" kind="LogStore" />
            </resourcereferences>
          </role>
          <sCSPolicy>
            <sCSPolicyIDMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/WorkerRoleServiceInstances" />
            <sCSPolicyUpdateDomainMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/WorkerRoleServiceUpgradeDomains" />
            <sCSPolicyFaultDomainMoniker name="/CrawlerWorkerRole/CrawlerWorkerRoleGroup/WorkerRoleServiceFaultDomains" />
          </sCSPolicy>
        </groupHascomponents>
      </components>
      <sCSPolicy>
        <sCSPolicyUpdateDomain name="WorkerRoleServiceUpgradeDomains" defaultPolicy="[5,5,5]" />
        <sCSPolicyFaultDomain name="WorkerRoleServiceFaultDomains" defaultPolicy="[2,2,2]" />
        <sCSPolicyID name="WorkerRoleServiceInstances" defaultPolicy="[1,1,1]" />
      </sCSPolicy>
    </group>
  </groups>
</serviceModel>