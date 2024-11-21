﻿// ------------------------------------------------------------------------------
//  <auto-generated>
//      This code was generated by SpecFlow (https://www.specflow.org/).
//      SpecFlow Version:3.9.0.0
//      SpecFlow Generator Version:3.9.0.0
// 
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// ------------------------------------------------------------------------------
#region Designer generated code
#pragma warning disable
namespace UtilsTest.Mathematics.LinearAlgebra
{
    using TechTalk.SpecFlow;
    using System;
    using System.Linq;
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("TechTalk.SpecFlow", "3.9.0.0")]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute()]
    public partial class MatrixTestsFeature
    {
        
        private static TechTalk.SpecFlow.ITestRunner testRunner;
        
        private Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _testContext;
        
        private static string[] featureTags = ((string[])(null));
        
#line 1 "MatrixTests.feature"
#line hidden
        
        public virtual Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext
        {
            get
            {
                return this._testContext;
            }
            set
            {
                this._testContext = value;
            }
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.ClassInitializeAttribute()]
        public static void FeatureSetup(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext testContext)
        {
            testRunner = TechTalk.SpecFlow.TestRunnerManager.GetTestRunner();
            TechTalk.SpecFlow.FeatureInfo featureInfo = new TechTalk.SpecFlow.FeatureInfo(new System.Globalization.CultureInfo("en-US"), "Mathematics/LinearAlgebra", "MatrixTests", "A short summary of the feature", ProgrammingLanguage.CSharp, featureTags);
            testRunner.OnFeatureStart(featureInfo);
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.ClassCleanupAttribute()]
        public static void FeatureTearDown()
        {
            testRunner.OnFeatureEnd();
            testRunner = null;
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute()]
        public void TestInitialize()
        {
            if (((testRunner.FeatureContext != null) 
                        && (testRunner.FeatureContext.FeatureInfo.Title != "MatrixTests")))
            {
                global::UtilsTest.Mathematics.LinearAlgebra.MatrixTestsFeature.FeatureSetup(null);
            }
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute()]
        public void TestTearDown()
        {
            testRunner.OnScenarioEnd();
        }
        
        public void ScenarioInitialize(TechTalk.SpecFlow.ScenarioInfo scenarioInfo)
        {
            testRunner.OnScenarioInitialize(scenarioInfo);
            testRunner.ScenarioContext.ScenarioContainer.RegisterInstanceAs<Microsoft.VisualStudio.TestTools.UnitTesting.TestContext>(_testContext);
        }
        
        public void ScenarioStart()
        {
            testRunner.OnScenarioStart();
        }
        
        public void ScenarioCleanup()
        {
            testRunner.CollectScenarioErrors();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Matrix Additions")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void MatrixAdditions()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Matrix Additions", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 5
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table4 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table4.AddRow(new string[] {
                            "1",
                            "2"});
                table4.AddRow(new string[] {
                            "3",
                            "4"});
#line 6
 testRunner.Given("m1 is a matrix", ((string)(null)), table4, "Given ");
#line hidden
                TechTalk.SpecFlow.Table table5 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table5.AddRow(new string[] {
                            "5",
                            "6"});
                table5.AddRow(new string[] {
                            "7",
                            "8"});
#line 10
   testRunner.And("m2 is a matrix", ((string)(null)), table5, "And ");
#line hidden
#line 14
 testRunner.When("I compute matrix m3 = m1 + m2", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "When ");
#line hidden
#line 15
 testRunner.When("I compute matrix m4 = m2 + m1", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "When ");
#line hidden
                TechTalk.SpecFlow.Table table6 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table6.AddRow(new string[] {
                            "6",
                            "8"});
                table6.AddRow(new string[] {
                            "10",
                            "12"});
#line 16
 testRunner.Then("I expect matrix m3 equals", ((string)(null)), table6, "Then ");
#line hidden
#line 20
    testRunner.And("I expect m3 = m4", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
#line 21
 testRunner.And("det(m1) = -2", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
#line 22
 testRunner.And("det(m2) = -2", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
#line 23
 testRunner.And("det(m3) = -8", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
            }
            this.ScenarioCleanup();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Matrix Multiplications")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void MatrixMultiplications()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Matrix Multiplications", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 25
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table7 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table7.AddRow(new string[] {
                            "1",
                            "2"});
                table7.AddRow(new string[] {
                            "3",
                            "4"});
#line 26
 testRunner.Given("m1 is a matrix", ((string)(null)), table7, "Given ");
#line hidden
                TechTalk.SpecFlow.Table table8 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table8.AddRow(new string[] {
                            "5",
                            "6"});
                table8.AddRow(new string[] {
                            "7",
                            "8"});
#line 30
   testRunner.And("m2 is a matrix", ((string)(null)), table8, "And ");
#line hidden
#line 34
 testRunner.When("I compute matrix m = m1 * m2", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "When ");
#line hidden
                TechTalk.SpecFlow.Table table9 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table9.AddRow(new string[] {
                            "19",
                            "22"});
                table9.AddRow(new string[] {
                            "43",
                            "50"});
#line 35
 testRunner.Then("I expect matrix m equals", ((string)(null)), table9, "Then ");
#line hidden
#line 39
 testRunner.And("det(m) = 4", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
#line 40
 testRunner.When("I compute matrix m = m2 * m1", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "When ");
#line hidden
                TechTalk.SpecFlow.Table table10 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table10.AddRow(new string[] {
                            "23",
                            "34"});
                table10.AddRow(new string[] {
                            "31",
                            "46"});
#line 41
 testRunner.Then("I expect matrix m equals", ((string)(null)), table10, "Then ");
#line hidden
#line 45
 testRunner.And("det(m) = 4", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "And ");
#line hidden
            }
            this.ScenarioCleanup();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Vector mulitplication")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void VectorMulitplication()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Vector mulitplication", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 47
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table11 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2"});
                table11.AddRow(new string[] {
                            "1",
                            "2"});
                table11.AddRow(new string[] {
                            "3",
                            "4"});
#line 48
 testRunner.Given("m1 is a matrix", ((string)(null)), table11, "Given ");
#line hidden
                TechTalk.SpecFlow.Table table12 = new TechTalk.SpecFlow.Table(new string[] {
                            "v1",
                            "vr"});
                table12.AddRow(new string[] {
                            "5",
                            "19"});
                table12.AddRow(new string[] {
                            "7",
                            "43"});
#line 52
 testRunner.And("Given these vectors", ((string)(null)), table12, "And ");
#line hidden
#line 56
 testRunner.When("I compute vector vt = m1 * v1", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "When ");
#line hidden
#line 57
 testRunner.Then("I expect vr = vt", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "Then ");
#line hidden
            }
            this.ScenarioCleanup();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Matrix determinant 3*3")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void MatrixDeterminant33()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Matrix determinant 3*3", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 60
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table13 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2",
                            "C3"});
                table13.AddRow(new string[] {
                            "12",
                            "10",
                            "9"});
                table13.AddRow(new string[] {
                            "2",
                            "8",
                            "6"});
                table13.AddRow(new string[] {
                            "1",
                            "13",
                            "14"});
#line 61
 testRunner.Given("m is a matrix", ((string)(null)), table13, "Given ");
#line hidden
#line 66
 testRunner.Then("det(m) = 350", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "Then ");
#line hidden
            }
            this.ScenarioCleanup();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Matrix determinant 4*4")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void MatrixDeterminant44()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Matrix determinant 4*4", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 68
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table14 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2",
                            "C3",
                            "C4"});
                table14.AddRow(new string[] {
                            "12",
                            "10",
                            "9",
                            "-12"});
                table14.AddRow(new string[] {
                            "2",
                            "-8",
                            "6",
                            "4"});
                table14.AddRow(new string[] {
                            "1",
                            "-13",
                            "14",
                            "2"});
                table14.AddRow(new string[] {
                            "1",
                            "8",
                            "-6",
                            "7"});
#line 69
 testRunner.Given("m is a matrix", ((string)(null)), table14, "Given ");
#line hidden
#line 75
 testRunner.Then("det(m) = -7622", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "Then ");
#line hidden
            }
            this.ScenarioCleanup();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Matrix determinant 5*5")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "MatrixTests")]
        public void MatrixDeterminant55()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            TechTalk.SpecFlow.ScenarioInfo scenarioInfo = new TechTalk.SpecFlow.ScenarioInfo("Matrix determinant 5*5", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 77
this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                this.ScenarioStart();
                TechTalk.SpecFlow.Table table15 = new TechTalk.SpecFlow.Table(new string[] {
                            "C1",
                            "C2",
                            "C3",
                            "C4",
                            "C5"});
                table15.AddRow(new string[] {
                            "12",
                            "10",
                            "9",
                            "-12",
                            "2"});
                table15.AddRow(new string[] {
                            "2",
                            "-8",
                            "6",
                            "4",
                            "-4"});
                table15.AddRow(new string[] {
                            "1",
                            "-13",
                            "14",
                            "2",
                            "4"});
                table15.AddRow(new string[] {
                            "1",
                            "8",
                            "-6",
                            "7",
                            "4"});
                table15.AddRow(new string[] {
                            "-1",
                            "-2",
                            "7",
                            "14",
                            "-18"});
#line 78
 testRunner.Given("m is a matrix", ((string)(null)), table15, "Given ");
#line hidden
#line 85
 testRunner.Then("det(m) = 294064", ((string)(null)), ((TechTalk.SpecFlow.Table)(null)), "Then ");
#line hidden
            }
            this.ScenarioCleanup();
        }
    }
}
#pragma warning restore
#endregion
