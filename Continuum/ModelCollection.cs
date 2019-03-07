﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;

namespace ContinuumNS
{
    [Serializable()]
    public class ModelCollection

    {
        public Model[,] models;  // List of models; i = Model ind (0 = default), j = radius ind        
        public double maxElevAllowed = 300; // Maximum elevation difference between predictor and target site for wind speed estimate to be formed
        public double maxP10ExpoAllowed = 200; // Maximum P10 exposure difference between predictor and target site for wind speed estimate to be formed
             
        public struct WS_Est_Struct {
            public double[] sectorWS;
            public double[,] sectorWS_AtNodes;
        }

        public struct Coeff_Delta_WS {
            public double coeff;
            public string flowType;  // Downhill, uphill, speed-up, valley or turbulent
            public double deltaWS_Expo;
            public string expoOrRough;  // specifies whether refers to exposure model or surface roughness model "Expo" or "SRDH"
        }

        public int ModelCount
        {
            get { if (models == null)
                    return 0;
                else return models.GetUpperBound(0) + 1; }
        }        

        public int RadiiCount
        {
            get
            {
                if (models == null)
                    return 0;
                else
                    return models.GetUpperBound(1) + 1;
            }
        }
    
        public void ClearAll() {
                //  Clears all models including site-calibrated
                models = null;                
        }               

        public void ClearAllExceptDefaultAndImported() {
            // Clears all models except default and imported models
            Model[] importedModel = null;

            if (ModelCount > 0) {
                Model[,] newModel = null;
                int haveImported = GetImportedModelInd();

                if (haveImported != -1)
                {
                    newModel = new Model[2, RadiiCount];
                    importedModel = new Model[RadiiCount];
                }
                else
                    newModel = new Model[1, RadiiCount];

                for (int i = 0; i < ModelCount; i++)
                {
                    if (models[i, 0].isCalibrated == false)
                    {
                        for (int j = 0; j < RadiiCount; j++)
                            newModel[0, j] = models[i, j];
                    }
                    else if (models[i, 0].isImported) {
                        for (int j = 0; j <= RadiiCount - 1; j++)                        
                            newModel[1, j] = models[i, j];                            
                        
                    }
                }

                models = newModel;
            }                        
        }

        public void ClearAllExceptImported()
        {
            // Clears all models except imported models
            Model[] importedModel = null;

            if (ModelCount > 0)
            {
                Model[,] newModel = null;
                int haveImported = GetImportedModelInd();

                if (haveImported != -1)
                {
                    newModel = new Model[2, RadiiCount];
                    importedModel = new Model[RadiiCount];
                }
                
                for (int i = 0; i < ModelCount; i++)
                {
                    if (models[i, 0].isImported)
                    {
                        for (int j = 0; j <= RadiiCount - 1; j++)                        
                            newModel[1, j] = models[i, j]; 
                    }
                }

                models = newModel;
            }            
        }

        public void ClearImported() { 
            // Clears all imported models
            if (ModelCount > 0 ) {
                Model[,] newModel = null;
                int modelInd = 0;

                for (int i = 0; i < ModelCount; i++)
                    if (models[i, 0].isImported == false)
                        modelInd++;

                newModel = new Model[modelInd, RadiiCount];
                modelInd = 0;

                for (int i = 0; i < ModelCount; i++)
                { 
                    if (models[i, 0].isImported == false)
                    {
                        for (int j = 0; j < RadiiCount; j++)
                            newModel[modelInd, j] = models[i, j];

                        modelInd++;
                    }
                }

                models = newModel;
            }               
            
        }       

        public void UpdateDefaultModel(Continuum thisInst)
        {
            // Updates the default model with new met list and resets the met cross-prediction errors
            string[] metsUsed = thisInst.metList.GetMetsUsed();
            int numRadii = thisInst.radiiList.ThisCount;
            int numWD = thisInst.metList.numWD;

            if (ModelCount > 0)
            {
                for (int j = 0; j < numRadii; j++)
                {
                    models[0, j].RMS_WS_Est = 0;
                    models[0, j].RMS_Sect_WS_Est = new double[numWD];
                    models[0, j].metsUsed = metsUsed;
                }

                for (int i = 0; i < thisInst.metPairList.PairCount; i++)
                    for (int j = 0; j <= numRadii - 1; j++)
                        thisInst.metPairList.metPairs[i].WS_Pred[0, j].model = models[0, j];

            }
            else { // create default model
                Model[] model = new Model[numRadii];
                
                for (int i = 0; i <= numRadii - 1; i++)
                {
                    model[i] = new Model();
                    model[i].SizeArrays(numWD);

                    model[i].setDefaultModelCoeffs(numWD);
                    model[i].SetDefaultLimits();
                    model[i].radius = thisInst.radiiList.investItem[i].radius;
                    model[i].metsUsed = metsUsed;
                    model[i].isCalibrated = false;
                }

                // Create new UWDW model in UWDW collection
                AddModel(model);
            }                        
            
        }

        public void AddModel(Model[] newModel) {
            // Adds a model to the list of models
            int numRadii = newModel.Length;

            Model[,] tempModels = new Model[ModelCount, numRadii];

            // Copy existing UW&DW models into temp array
            for (int i = 0; i < ModelCount; i++)
                for (int j = 0; j <= numRadii - 1; j++)
                    tempModels[i, j] = models[i, j];

            models = new Model[ModelCount + 1, numRadii];

            // Copy UW&DW models back in
            for (int i = 0; i <= ModelCount - 2; i++)
                for (int j = 0; j <= numRadii - 1; j++)
                    models[i, j] = tempModels[i, j];               
            
            if (ModelCount == 0 )
            {
                for (int i = 0; i <= numRadii - 1; i++)
                    models[0, i] = newModel[i];
            }
            else {
                for (int i = 0; i < numRadii; i++)
                    models[ModelCount - 1, i] = newModel[i];                
            }

        }

        public double[] GetModelWeights(Model[] models)
        {
            // returns model weight based on overall RMS error found for each (i.e. R = 4000, 6000, 8000, 10000)
            // 
            int numModels = models.Count();
            double[] theseWeights = new double[numModels];
            if (numModels == 0)
                return theseWeights;

            double minRMS = 1000;
            double maxRMS = 0;

            // Finds the min and max met cross-prediction error of models()
            for (int i = 0; i < models.Length; i++)
            {
                if (models[i].isCalibrated == true)
                {                    
                    if (models[i].RMS_WS_Est < minRMS)
                        minRMS = models[i].RMS_WS_Est;

                    if (models[i].RMS_WS_Est > maxRMS)
                        maxRMS = models[i].RMS_WS_Est;
                }
                else
                {
                    theseWeights[i] = 1;
                }
            }

            for (int i = 0; i < models.Length; i++)
            {
                if (models[i].isCalibrated == true)
                {
                    double weightConst = 0.75f;
                    double slope = -weightConst / (maxRMS - minRMS);
                    double intercept = 1 + weightConst * minRMS / (maxRMS - minRMS);

                    if (minRMS != maxRMS)
                        theseWeights[i] = slope * models[i].RMS_WS_Est + intercept;
                    else
                        theseWeights[i] = 1;
                }
            }

            return theseWeights;
        }

        public double[,] GetWS_EstWeights(Met[] predictorMets, Nodes targetNode, Model[] models, double[] windRose)
        {
            // Calculates and returns wind speed estimates weights at a target site for each predictor met and each models() 
            int numPredMets = predictorMets.Length;
            double[,] weights = new double[1, 1];
            int slopeFactor = 1;
            
            double minRMS = 1000;
            double maxRMS = 0;
            double RMS_Weight = 0;
            
            int numWD = windRose.Length;                     

            double weightConst = 0.75f;
            double[] modelsRMS = new double[models.Length];

            // Finds the min and max met cross-prediction error of models()
            for (int i = 0; i < models.Length; i++)
            { 
                if (models[i].isCalibrated == true)
                {
                    modelsRMS[i] = models[i].RMS_WS_Est;
                    if (modelsRMS[i] < minRMS)
                        minRMS = modelsRMS[i];

                    if (modelsRMS[i] > maxRMS)
                        maxRMS = modelsRMS[i];                    
                }
            }

            // slope and intercept for the RMS weight. weightConst = 0.75 so that minimum RMS weight = 0.25
            double slope = -weightConst / (maxRMS - minRMS);
            double intercept = 1 + weightConst * minRMS / (maxRMS - minRMS);

            if (numPredMets > 1)
            {
                weights = new double[numPredMets, models.Length];
                for (int radiusIndex = 0; radiusIndex < models.Length; radiusIndex++)
                {
                    double DW_Diff = 0;
                    double UW_Diff = 0;
                    // Calculates average difference in P10 exposure between predictor met and target site (weighted by wind rose) and finds weight where higher weight
                    // is applied to met sites with more similar terrain complexity (i.e. P10 exposure)

                    double sumUWDW_Diff = 0;
                    for (int i = 0; i < numPredMets; i++)
                    {
                        DW_Diff = 0;
                        UW_Diff = 0;
                        for (int WD = 0; WD < numWD; WD++)
                        {
                            DW_Diff = DW_Diff + Math.Abs(predictorMets[i].gridStats.stats[radiusIndex].P10_DW[WD] - targetNode.gridStats.stats[radiusIndex].P10_DW[WD]) * windRose[WD];
                            UW_Diff = UW_Diff + Math.Abs(predictorMets[i].gridStats.stats[radiusIndex].P10_UW[WD] - targetNode.gridStats.stats[radiusIndex].P10_UW[WD]) * windRose[WD];
                        }
                        sumUWDW_Diff = sumUWDW_Diff + DW_Diff + UW_Diff;
                    }

                    if (models[radiusIndex].isCalibrated == true && minRMS != maxRMS)
                        RMS_Weight = slope * modelsRMS[radiusIndex] + intercept;
                    else
                        RMS_Weight = 1;

                    for (int i = 0; i <= numPredMets - 1; i++)
                    {
                        DW_Diff = 0;
                        UW_Diff = 0;

                        for (int WD = 0; WD <= numWD - 1; WD++)
                        {
                            DW_Diff = DW_Diff + Math.Abs(predictorMets[i].gridStats.stats[radiusIndex].P10_DW[WD] - targetNode.gridStats.stats[radiusIndex].P10_DW[WD]) * windRose[WD];
                            UW_Diff = UW_Diff + Math.Abs(predictorMets[i].gridStats.stats[radiusIndex].P10_UW[WD] - targetNode.gridStats.stats[radiusIndex].P10_UW[WD]) * windRose[WD];
                        }

                        if (sumUWDW_Diff > 0)
                        {
                            weights[i, radiusIndex] = 1 - slopeFactor * (DW_Diff + UW_Diff) / sumUWDW_Diff;
                            if (weights[i, radiusIndex] < 0)
                                weights[i, radiusIndex] = 0;
                        }
                        else
                            weights[i, radiusIndex] = 1;

                        if (models[radiusIndex].isCalibrated == true) weights[i, radiusIndex] = weights[i, radiusIndex] * RMS_Weight;
                    }
                }
            }
            else if (numPredMets == 1)
            {                
                weights = new double[numPredMets, models.Length];
                for (int i = 0; i < models.Length; i++)
                    weights[0, i] = 1;

            }

            return weights;
        }

        public bool IterationAlreadyDone(Continuum thisInst, string[] metsUsed, int minRadius, int maxRadius)
        {
            //  Returns false if site-calibration hasn//t been performed yet
            bool alreadyDone = false;
            bool sameMets = false;

            for (int i = 0; i < ModelCount; i++)
            {
                sameMets = thisInst.metList.sameMets(metsUsed, models[i, 0].metsUsed);

                if (sameMets == true && models[i, 0].radius == minRadius && models[i, RadiiCount - 1].radius == maxRadius && 
                    models[i, 0].isCalibrated == true ) {
                    alreadyDone = true;
                    break;
                }
            }

            return alreadyDone;
        }              

        public int GetImportedModelInd()
        {
            // Returns index of imported model in list of models
            int importedInd = -1;

            for (int i = 0; i < ModelCount; i++)
                if (models[i, 0] != null)
                {
                    if (models[i, 0].isImported == true)
                    {
                        importedInd = i;
                        break;
                    }
                }                

            return importedInd;
        }

        public Model[] GetModels(Continuum thisInst, string[] metsUsed, int minRadius, int maxRadius, bool isCalibrated)
        {
            // Returns models with specified mets, radius range and site-calibrated vs. default
            Model[] thisModel = new Model[RadiiCount];
            bool sameMets = false;
            
            for (int i = 0; i < ModelCount; i++)
            {
                sameMets = thisInst.metList.sameMets(metsUsed, models[i, 0].metsUsed);
                if (sameMets == true && isCalibrated == models[i, 0].isCalibrated)
                {
                    for (int j = 0; j < RadiiCount; j++)
                        thisModel[j] = models[i, j];

                    break;
                }     
                else if (isCalibrated == false && models[i, 0].isCalibrated == false)
                {
                    for (int j = 0; j < RadiiCount; j++)
                        thisModel[j] = models[i, j];

                    break;
                }
            }

            if (thisModel[0] == null && metsUsed.Length > 1)
            {
                CreateModel(metsUsed, thisInst);
                for (int i = 0; i < ModelCount; i++)
                {
                    sameMets = thisInst.metList.sameMets(metsUsed, models[i, 0].metsUsed);
                    if (sameMets == true && isCalibrated == models[i, 0].isCalibrated)
                    {
                        for (int j = 0; j < RadiiCount; j++)
                            thisModel[j] = models[i, j];

                        break;
                    }
                }
            }
                

            return thisModel;
        }

        public bool IsSameModel(Model model1, Model model2)
        {
            //  Returns true if two models are identical
            bool isSameMets = false;
            bool isSame = true;
            int numWD;
            try
            {
                numWD = model1.downhill_A.Length;
            }
            catch 
            {
                isSame = false;
                return isSame;
            }

            if (model1 != null && model2 != null)
            {
                isSameMets = sameMets(model1.metsUsed, model2.metsUsed);
                for (int WD = 0; WD < numWD; WD++)
                {
                    try
                    {
                        if (isSameMets == true && model1.downhill_A[WD] == model2.downhill_A[WD] && model1.downhill_B[WD] == model2.downhill_B[WD] && model1.uphill_A[WD] == model2.uphill_A[WD] && model1.radius == model2.radius
                            && model1.uphill_B[WD] == model2.uphill_B[WD] && model1.UW_crit[WD] == model2.UW_crit[WD] && model1.spdUp_A[WD] == model2.spdUp_A[WD] && model1.spdUp_B[WD] == model2.spdUp_B[WD]
                            && model1.DH_Stab_A[WD] == model2.DH_Stab_A[WD] && model1.UH_Stab_A[WD] == model2.UH_Stab_A[WD] && model1.SU_Stab_A[WD] == model2.SU_Stab_A[WD] && model1.stabB[WD] == model2.stabB[WD]
                            && model1.isCalibrated == model2.isCalibrated && model1.isImported == model2.isImported ||
                            (model1.isCalibrated == false && model2.isCalibrated == false && model1.radius == model2.radius))

                            isSame = true;
                        else
                        {
                            isSame = false;
                            break;
                        }
                    }
                    catch 
                    {
                        isSame = false;
                        return isSame;
                    }
                }
            }
            else
                isSame = false;


            return isSame;
        }

        public bool sameMets(string[] metsUsed1, string[] metsUsed2)
        {
            //  Returns true if two lists of mets are identical
            string string1 = "";
            string string2 = "";
            bool sameMetSites = false;

            if (metsUsed1 != null && metsUsed2 != null)
            {
                if (metsUsed1.Length == metsUsed2.Length)
                {
                    for (int j = 0; j < metsUsed1.Length; j++)
                    {
                        string1 = string1 + metsUsed1[j].ToString();
                        string2 = string2 + metsUsed2[j].ToString();
                    }

                    if (string1 == string2)
                        sameMetSites = true;
                }
            }
            else
                sameMetSites = false;

            return sameMetSites;
        }

        public void FindSiteCalibratedModels(Continuum thisInst)
        {
            // With specified model settings and met list, finds site-calibrated model for each radius of investigation
           
            // need more than one met to create site-calibrate model
            if (thisInst.metList.ThisCount == 1)
                return;     
                    
            // Check to see if met exposures and cross-predictions have all been calculated
            if (thisInst.metList.ThisCount > 1)
                CreateModel(thisInst.metList.GetMetsUsed(), thisInst);                               
            
        }        

        public void ImportModelsCSV(Continuum thisInst)
        {
            // Opens model coefficient file and creates model with imported coefficients

            int numRadii = thisInst.radiiList.ThisCount;
            Model[] importModel = new Model[numRadii];
                        
            string thisString = "";
            string[] theseParams;
            bool useSR = false;
            bool usesFlowSep = false;
            StreamReader sr = new StreamReader("C:\\");

            if (thisInst.ofdImportCoeffs.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(thisInst.ofdImportCoeffs.FileName) == false)
                {
                    MessageBox.Show("Error opening the file. Check that it's not open in another program.", "Continuum 2.2");
                    return;
                }

                try {                    
                    sr = new StreamReader(thisInst.ofdImportCoeffs.FileName);                }
                catch  {
                    MessageBox.Show("Error opening the file. Check that it's not open in another program.",  "Continuum 2.2");                    
                    return;
                }

                try {
                    thisString = sr.ReadLine(); // Continuum 2.2 Model Parameters                    
                    thisString = sr.ReadLine(); // Date
                    thisString = sr.ReadLine(); // Site-calibrated Model using X mets

                    int firstParanth = thisString.IndexOf("(");
                    int lastParenth = thisString.IndexOf(")");
                    string[] theseMets = thisString.Substring(firstParanth + 2, lastParenth - firstParanth - 1).Split(Convert.ToChar(" "));

                    Model defaultModel = new Model();
                    defaultModel.SizeArrays(15); // it's okay that we're hardcoding numWD = 15 here since we're just doing this to set the default coefficients.The array is resized.
                    defaultModel.setDefaultModelCoeffs(15);

                    for (int i = 0; i < numRadii; i++)
                    {
                        importModel[i] = new Model();
                        importModel[i].radius = thisInst.radiiList.investItem[i].radius;
                        importModel[i].isImported = true;
                        importModel[i].isCalibrated = false;
                        importModel[i].SetDefaultLimits(); // max diff in expo and elev
                        importModel[i].metsUsed = theseMets;

                        thisString = sr.ReadLine(); // radius
                        thisString = sr.ReadLine(); // RMS heading

                        thisString = sr.ReadLine(); // RMS error
                        importModel[i].RMS_WS_Est = Convert.ToSingle(thisString.Substring(1, thisString.Length - 1)) / 100;

                        thisString = sr.ReadLine(); // Model Headings

                        string[] theseHeaders = thisString.Split(Convert.ToChar(","));

                        for (int head_ind = 0; head_ind <= theseHeaders.Length - 1; head_ind++)
                        {
                            if (theseHeaders[head_ind] == "DH Stability_A")
                                useSR = true;
                            else if (theseHeaders[head_ind] == "sep_A_DW")
                                usesFlowSep = true;
                        }

                        int WD_Ind = 0;

                        while (thisString != "")
                        {
                            thisString = sr.ReadLine();
                            theseParams = thisString.Split(Convert.ToChar(","));

                            if (thisString != "")
                            {
                                Array.Resize(ref importModel[i].RMS_Sect_WS_Est, WD_Ind + 1);
                                Array.Resize(ref importModel[i].downhill_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].downhill_B, WD_Ind + 1);
                                Array.Resize(ref importModel[i].uphill_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].uphill_B, WD_Ind + 1);
                                Array.Resize(ref importModel[i].UW_crit, WD_Ind + 1);
                                Array.Resize(ref importModel[i].spdUp_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].spdUp_B, WD_Ind + 1);
                                Array.Resize(ref importModel[i].DH_Stab_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].UH_Stab_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].SU_Stab_A, WD_Ind + 1);
                                Array.Resize(ref importModel[i].stabB, WD_Ind + 1);
                                Array.Resize(ref importModel[i].sep_A_DW, WD_Ind + 1);
                                Array.Resize(ref importModel[i].sep_B_DW, WD_Ind + 1);
                                Array.Resize(ref importModel[i].turbWS_Fact, WD_Ind + 1);
                                Array.Resize(ref importModel[i].sepCrit, WD_Ind + 1);
                                Array.Resize(ref importModel[i].Sep_crit_WS, WD_Ind + 1);

                                importModel[i].RMS_Sect_WS_Est[WD_Ind] = Convert.ToSingle(theseParams[1].Substring(2, theseParams[1].Length - 4)) / 100;
                                importModel[i].downhill_A[WD_Ind] = Convert.ToSingle(theseParams[2]);
                                importModel[i].downhill_B[WD_Ind] = Convert.ToSingle(theseParams[3]);
                                importModel[i].uphill_A[WD_Ind] = Convert.ToSingle(theseParams[4]);
                                importModel[i].uphill_B[WD_Ind] = Convert.ToSingle(theseParams[5]);
                                importModel[i].UW_crit[WD_Ind] = Convert.ToSingle(theseParams[6]);
                                importModel[i].spdUp_A[WD_Ind] = Convert.ToSingle(theseParams[7]);
                                importModel[i].spdUp_B[WD_Ind] = Convert.ToSingle(theseParams[8]);

                                if (useSR == true && usesFlowSep == false) {
                                    importModel[i].DH_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[9]);
                                    importModel[i].UH_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[10]);
                                    importModel[i].SU_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[11]);
                                    importModel[i].sep_A_DW[WD_Ind] = defaultModel.sep_A_DW[0];
                                    importModel[i].sep_B_DW[WD_Ind] = defaultModel.sep_B_DW[0];
                                    importModel[i].turbWS_Fact[WD_Ind] = defaultModel.turbWS_Fact[0];
                                    importModel[i].sepCrit[WD_Ind] = defaultModel.sepCrit[0];
                                    importModel[i].Sep_crit_WS[WD_Ind] = defaultModel.Sep_crit_WS[0];
                                }
                                else if (useSR == false && usesFlowSep == true) {
                                    importModel[i].DH_Stab_A[WD_Ind] = defaultModel.DH_Stab_A[0];
                                    importModel[i].UH_Stab_A[WD_Ind] = defaultModel.UH_Stab_A[0];
                                    importModel[i].SU_Stab_A[WD_Ind] = defaultModel.SU_Stab_A[0];
                                    importModel[i].sep_A_DW[WD_Ind] = Convert.ToSingle(theseParams[9]);
                                    importModel[i].sep_B_DW[WD_Ind] = Convert.ToSingle(theseParams[10]);
                                    importModel[i].turbWS_Fact[WD_Ind] = Convert.ToSingle(theseParams[11]);
                                    importModel[i].sepCrit[WD_Ind] = Convert.ToSingle(theseParams[12]);
                                    importModel[i].Sep_crit_WS[WD_Ind] = Convert.ToSingle(theseParams[13]);
                                }
                                else if (useSR == true && usesFlowSep == true) {
                                    importModel[i].DH_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[9]);
                                    importModel[i].UH_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[10]);
                                    importModel[i].SU_Stab_A[WD_Ind] = Convert.ToSingle(theseParams[11]);
                                    importModel[i].sep_A_DW[WD_Ind] = Convert.ToSingle(theseParams[12]);
                                    importModel[i].sep_B_DW[WD_Ind] = Convert.ToSingle(theseParams[13]);
                                    importModel[i].turbWS_Fact[WD_Ind] = Convert.ToSingle(theseParams[14]);
                                    importModel[i].sepCrit[WD_Ind] = Convert.ToSingle(theseParams[15]);
                                    importModel[i].Sep_crit_WS[WD_Ind] = Convert.ToSingle(theseParams[16]);
                                }
                                else {
                                    importModel[i].DH_Stab_A[WD_Ind] = defaultModel.DH_Stab_A[0];
                                    importModel[i].UH_Stab_A[WD_Ind] = defaultModel.UH_Stab_A[0];
                                    importModel[i].SU_Stab_A[WD_Ind] = defaultModel.SU_Stab_A[0];
                                    importModel[i].sep_A_DW[WD_Ind] = defaultModel.sep_A_DW[0];
                                    importModel[i].sep_B_DW[WD_Ind] = defaultModel.sep_B_DW[0];
                                    importModel[i].turbWS_Fact[WD_Ind] = defaultModel.turbWS_Fact[0];
                                    importModel[i].sepCrit[WD_Ind] = defaultModel.sepCrit[0];
                                    importModel[i].Sep_crit_WS[WD_Ind] = defaultModel.Sep_crit_WS[0];
                                }


                            }
                            WD_Ind++;
                        }

                        WD_Ind--;

                        // Check to see if the same length  wind rose entered
                        if (thisInst.metList.ThisCount > 0) {
                            if (thisInst.metList.numWD != WD_Ind) {
                                MessageBox.Show("The imported model h a different number of WD sectors than the entered met site. Check your inputs.", "Continuum 2.2");
                                sr.Close();
                                return;
                            }
                        }
                        

                    }
                }
                catch {
                    sr.Close();
                    return;
                }

                sr.Close();

                // if ( no mets have been added yet then default model doesn//t exist so create it now
                Model[] model = new Model[numRadii];
                bool haveDefault = false;
                int defaultInd = 0;

                for (int i = 0; i < ModelCount; i++) { 
                    if (models[i, 0].isCalibrated) {
                        haveDefault = true;
                        defaultInd = i;
                        break;
                    }
                }

                if (haveDefault == false)
                {
                    // Create default model if doesn't exist
                    for (int i = 0; i < numRadii; i++) {
                        model[i] = new Model();
                        model[i].SizeArrays(thisInst.metList.numWD);

                        model[i].setDefaultModelCoeffs(thisInst.metList.numWD);
                        model[i].SetDefaultLimits();
                        model[i].radius = thisInst.radiiList.investItem[i].radius;
                        model[i].metsUsed = thisInst.metList.GetMetsUsed();
                        model[i].isCalibrated = true;
                    }

                    // Create new UWDW model in UWDW collection
                    AddModel(model);
                }

                AddModel(importModel);
                
            }
        }

        public Model[] CreateModel(string[] metsUsed, Continuum thisInst)
        {
            // Creates and returns site-calibrated models using specified mets and model settings
            int numPairs = thisInst.metPairList.PairCount;

            int minRadius = thisInst.radiiList.investItem[0].radius;
            int maxRadius = thisInst.radiiList.GetMaxRadius();
            int thisRadius;
            int numRadii = thisInst.radiiList.ThisCount;
            Nodes[] pathOfNodes;

            int numMets = metsUsed.Length;

            int numWD;
            try {
                numWD = thisInst.metList.metItem[0].sectorWS_Ratio.Length;
            }
            catch { 
                return null;
            }

            int WS_PredInd = 0;
            Model[] models;
            bool modelAlreadyCreated = false;

            if (numMets == 1)
            {                
                if (ModelCount == 0)
                {
                    models = new Model[numRadii];

                    for (int i = 0; i < numRadii; i++)
                    {
                        models[i] = new Model(); 
                        models[i].SizeArrays(numWD);
                        thisRadius = thisInst.radiiList.investItem[i].radius;
                        models[i].SetDefaultLimits();

                        models[i].radius = thisRadius;
                        models[i].metsUsed = metsUsed;

                        models[i].setDefaultModelCoeffs(numWD); // DW A and B, UW A and B, UW slope
                        models[i].isCalibrated = false;
                    }
                    AddModel(models);

                    for (int j = 0; j < numPairs; j++)
                        thisInst.metPairList.metPairs[j].AddWS_Pred(models);

                    for (int i = 0; i < numRadii; i++) {
                        for (int j = 0; j < numPairs; j++) {
                            // Need to create new WS_pred
                            pathOfNodes = thisInst.metPairList.metPairs[j].WS_Pred[0, i].nodePath;
                            thisRadius = thisInst.radiiList.investItem[i].radius;
                            thisInst.metPairList.metPairs[j].AddNodesToWS_Pred(models, pathOfNodes, thisRadius, this);
                        }
                    }

                    // Do WS estimates with new models
                    for (int j = 0; j < numPairs; j++) {
                        WS_PredInd = thisInst.metPairList.metPairs[j].GetWS_PredInd(models, this);

                        for (int radiusIndex = 0; radiusIndex < numRadii; radiusIndex++)
                            thisInst.metPairList.metPairs[j].DoMetCrossPred(WS_PredInd, radiusIndex, thisInst);

                    }

                    // Calculate RMS with UW&DW models
                    CalcRMS_Overall_and_Sectorwise(ref models, thisInst);
                }
                else if (numMets != thisInst.metList.ThisCount) {
                    models = new Model[numRadii];

                    for (int i = 0; i < numRadii; i++) {
                        models[i] = new Model(); 
                        models[i].SizeArrays(numWD);
                        thisRadius = thisInst.radiiList.investItem[i].radius;
                        models[i].SetDefaultLimits();

                        models[i].radius = thisRadius;
                        models[i].metsUsed = metsUsed;

                        models[i].setDefaultModelCoeffs(numWD); // DW A and B, UW A and B, UW slope
                        models[i].isCalibrated = false;
                    }
                }
                else
                    models = GetModels(thisInst, metsUsed, minRadius, maxRadius, true);
            }
            else {
                modelAlreadyCreated = IterationAlreadyDone(thisInst, metsUsed, minRadius, maxRadius);

                if (modelAlreadyCreated == false)
                {
                    models = new Model[numRadii];

                    for (int i = 0; i < numRadii; i++)
                    {
                        models[i] = new Model(); // needed?
                        models[i].SizeArrays(numWD);
                        thisRadius = thisInst.radiiList.investItem[i].radius;
                        models[i].SetDefaultLimits();
                        models[i].radius = thisRadius;
                        models[i].metsUsed = metsUsed;
                        models[i].setDefaultModelCoeffs(numWD); // DW A and B, UW A and B, UW slope
                        models[i].isCalibrated = true;
                    }

                    AddModel(models);

                    for (int j = 0; j < numPairs; j++)
                        thisInst.metPairList.metPairs[j].AddWS_Pred(models);

                    for (int i = 0; i < numRadii; i++)
                    {
                        for (int j = 0; j < numPairs; j++)
                        {
                            // Need to create new WS_pred
                            pathOfNodes = thisInst.metPairList.metPairs[j].WS_Pred[0, i].nodePath;
                            thisRadius = thisInst.radiiList.investItem[i].radius;
                            thisInst.metPairList.metPairs[j].AddNodesToWS_Pred(models, pathOfNodes, thisRadius, this);
                        }

                        thisInst.metPairList.SweepFindMinError(models[i], thisInst);
                    }

                    // Do WS estimates with new models
                    for (int j = 0; j < numPairs; j++)
                    {
                        WS_PredInd = thisInst.metPairList.metPairs[j].GetWS_PredInd(models, this);

                        for (int radiusIndex = 0; radiusIndex < numRadii; radiusIndex++)
                            thisInst.metPairList.metPairs[j].DoMetCrossPred(WS_PredInd, radiusIndex, thisInst);
                    }

                    FindModelBounds(ref models, thisInst.metPairList, thisInst.radiiList); // finds P10 Expo Min/Max
                    // Calculate RMS with UW&DW models
                    CalcRMS_Overall_and_Sectorwise(ref models, thisInst);
                }

                else
                    models = GetModels(thisInst, metsUsed, minRadius, maxRadius, true);                
            }

            return models;
        }

    
    public double GetTotalWS(Coeff_Delta_WS[] coeffsDelta) {
            //  Calculates and returns the total change in wind speed
            double totalWS = 0;
            int numCoeffs = 0;

            if (coeffsDelta != null)
                numCoeffs = coeffsDelta.Length;


            if (numCoeffs > 0)
                totalWS = coeffsDelta[numCoeffs - 1].deltaWS_Expo;

            return totalWS;
    }

        public WS_Est_Struct DoWS_Estimate(Met startMet, Nodes endNode, Nodes[] pathOfNodes, int radiusIndex, Model thisModel, Continuum thisInst)
        {
            // Performs wind speed estimate along path of nodes using specified model
            WS_Est_Struct WS_Est_Return = new WS_Est_Struct();

            // Calculates wind speed from Met 1 to Met 2 along path of nodes 
            int numNodes = 0;
            int nodeInd = 0;            
          //  double UW_CW_Grade = 0;
          //  double UW_PL_Grade = 0;

            double avgUW = 0;
            double avgDW = 0;
            double avgP10DW = 0;
            double avgP10UW = 0;

            double UW_1 = 0; // UW exposure at met or node 1
            double DW_1 = 0;
            double UW_2 = 0;
            double DW_2 = 0;

            double WS_1 = 0; // WS at met or node 1
            
            double UW_SR_1 = 0; // UW surface roughness at met or node 1
            double DW_SR_1 = 0;
            double UW_SR_2 = 0;
            double DW_SR_2 = 0;

            double UW_DH_1 = 0; // UW displacement height at met or node 1
            double DW_DH_1 = 0;
            double UW_DH_2 = 0;
            double DW_DH_2 = 0;

            Coeff_Delta_WS[] coeffsDelta; 
            double deltaWS_UWExpo = 0; // change in wind speed due to change in UW exposure
            double deltaWS_DWExpo = 0; // change in wind speed due to change in DW exposure
            double deltaWS_UW_SR = 0; // change in wind speed due to change in UW surface roughness and displacement height
            double deltaWS_DW_SR = 0; // change in wind speed due to change in DW surface roughness and displacement height

            double UW_Stab_Corr_1 = 0;
            double UW_Stab_Corr_2 = 0;
            double DW_Stab_Corr_1 = 0;
            double DW_Stab_Corr_2 = 0;

            NodeCollection nodeList = new NodeCollection();
            Nodes thisNode;
            Nodes node1;
            Nodes node2;
            NodeCollection.Node_UTMs nodeUTM1 = new NodeCollection.Node_UTMs();
            nodeUTM1.UTMX = startMet.UTMX;
            nodeUTM1.UTMY = startMet.UTMY;
            NodeCollection.Node_UTMs nodeUTM2 = new NodeCollection.Node_UTMs();
                        
            int numRadii = RadiiCount;
            int numWD = thisInst.metList.metItem[0].windRose.Length;

            NodeCollection.Sep_Nodes[] flowSepNodes = new NodeCollection.Sep_Nodes[2];

            if (pathOfNodes != null)
                numNodes = pathOfNodes.Length;

            WS_Est_Return.sectorWS = new double[numWD];
            double[] sectorWS = new double[numWD];
            double[] WS_Equiv;

            for (int WD = 0; WD < numWD; WD++)
                sectorWS[WD] = startMet.WS * startMet.sectorWS_Ratio[WD];
            
            if (numNodes > 0)
            {
                WS_Est_Return.sectorWS_AtNodes = new double[numNodes, numWD];
                thisNode = pathOfNodes[0];
                nodeUTM2.UTMX = thisNode.UTMX;
                nodeUTM2.UTMY = thisNode.UTMY;
                WS_Equiv = GetWS_Equiv(startMet.windRose, thisNode.windRose, sectorWS);

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                {
                    // Met to first node
                    if (thisNode.windRose == null) thisNode.windRose = thisInst.metList.GetInterpolatedWindRose(thisModel.metsUsed, thisNode.UTMX, thisNode.UTMY);

                    avgP10DW = (startMet.gridStats.stats[radiusIndex].P10_DW[WD_Ind] + thisNode.gridStats.stats[radiusIndex].P10_DW[WD_Ind]) / 2;
                    avgP10UW = (startMet.gridStats.stats[radiusIndex].P10_UW[WD_Ind] + thisNode.gridStats.stats[radiusIndex].P10_UW[WD_Ind]) / 2;

                    UW_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "Expo");
                    UW_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "Expo");
                    avgUW = (UW_1 + UW_2) / 2;

                    // to be used in flow around  vs. flow over hill algorithm
              //      UW_CW_Grade = (startMet.expo[radiusIndex].UW_P10CrossGrade[WD_Ind] + thisNode.expo[radiusIndex].UW_P10CrossGrade[WD_Ind]) / 2;
              //      UW_PL_Grade = (startMet.expo[radiusIndex].UW_ParallelGrade[WD_Ind] + thisNode.expo[radiusIndex].UW_ParallelGrade[WD_Ind]) / 2;

                    WS_1 = WS_Equiv[WD_Ind];

                    if (thisInst.topo.gotSR == true) {
                        UW_SR_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "SR");
                        UW_SR_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "SR");
                        UW_DH_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "DH");
                        UW_DH_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "DH");

                        DW_SR_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "SR");
                        DW_SR_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "SR");
                        DW_DH_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "DH");
                        DW_DH_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "DH");
                    }

                    DW_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "Expo");
                    DW_2 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "Expo");

                    avgDW = (DW_1 + DW_2) / 2;

                    if (thisInst.topo.useSepMod == true) flowSepNodes = nodeList.GetSepNodes1and2(startMet.flowSepNodes, thisNode.flowSepNodes, WD_Ind);

                    coeffsDelta = Get_DeltaWS_UW_Expo(UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, radiusIndex, startMet.flowSepNodes,
                                                           thisNode.flowSepNodes, WS_1, thisInst.topo.useSepMod, nodeUTM1, nodeUTM2);
                    deltaWS_UWExpo = GetTotalWS(coeffsDelta);
                    coeffsDelta = Get_DeltaWS_DW_Expo(WS_1, UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, thisInst.topo.useSepMod);
                    deltaWS_DWExpo = GetTotalWS(coeffsDelta);

                    if (thisInst.topo.gotSR == true) {
                        UW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_1, thisInst.topo.useSepMod, "UW");
                        UW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_2, thisInst.topo.useSepMod, "UW");
                        deltaWS_UW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, UW_SR_1, UW_SR_2, UW_DH_1, UW_DH_2, UW_Stab_Corr_1, UW_Stab_Corr_2);

                        DW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_1, thisInst.topo.useSepMod, "DW");
                        DW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_2, thisInst.topo.useSepMod, "DW");
                        deltaWS_DW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, DW_SR_1, DW_SR_2, DW_DH_1, DW_DH_2, DW_Stab_Corr_1, DW_Stab_Corr_2);
                    }

                    // Avg WS Estimate at first node
                    if ((WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR) > 0)
                        WS_Est_Return.sectorWS_AtNodes[0, WD_Ind] = WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR;
                    else
                        WS_Est_Return.sectorWS_AtNodes[0, WD_Ind] = 0.05f; // no negative wind speed estimates!                    
                }

                // Now do estimates up to met2
                nodeInd = 0;
                while (nodeInd + 1 < numNodes)
                {
                    node1 = pathOfNodes[nodeInd];
                    nodeUTM1.UTMX = pathOfNodes[nodeInd].UTMX;
                    nodeUTM1.UTMY = pathOfNodes[nodeInd].UTMY;

                    node2 = pathOfNodes[nodeInd + 1];
                    nodeUTM2.UTMX = pathOfNodes[nodeInd + 1].UTMX;
                    nodeUTM2.UTMY = pathOfNodes[nodeInd + 1].UTMY;

                    if (node1.windRose == null) node1.windRose = thisInst.metList.GetInterpolatedWindRose(thisModel.metsUsed, node1.UTMX, node1.UTMY);
                    if (node2.windRose == null) node2.windRose = thisInst.metList.GetInterpolatedWindRose(thisModel.metsUsed, node2.UTMX, node2.UTMY);

                    for (int WD_Ind = 0; WD_Ind <= numWD - 1; WD_Ind++)
                        sectorWS[WD_Ind] = WS_Est_Return.sectorWS_AtNodes[nodeInd, WD_Ind];

                    WS_Equiv = GetWS_Equiv(node1.windRose, node2.windRose, sectorWS);

                    for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                    {
                        avgP10DW = (node1.gridStats.stats[radiusIndex].P10_DW[WD_Ind] + node2.gridStats.stats[radiusIndex].P10_DW[WD_Ind]) / 2;
                        avgP10UW = (node1.gridStats.stats[radiusIndex].P10_UW[WD_Ind] + node2.gridStats.stats[radiusIndex].P10_UW[WD_Ind]) / 2;

                        // for flow around vs. flow over hill algorithm
                  //      UW_CW_Grade = (node1.expo[radiusIndex].UW_P10CrossGrade[WD_Ind] + node2.expo[radiusIndex].UW_P10CrossGrade[WD_Ind]) / 2;
                  //      UW_PL_Grade = (node1.expo[radiusIndex].UW_ParallelGrade[WD_Ind] + node2.expo[radiusIndex].UW_ParallelGrade[WD_Ind]) / 2;

                        UW_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "UW", "Expo");
                        UW_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "UW", "Expo");

                        avgUW = (UW_1 + UW_2) / 2;

                        WS_1 = WS_Equiv[WD_Ind];

                        if (thisInst.topo.gotSR == true) {
                            UW_SR_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "UW", "SR");
                            UW_SR_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "UW", "SR");
                            UW_DH_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "UW", "DH");
                            UW_DH_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "UW", "DH");

                            DW_SR_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "DW", "SR");
                            DW_SR_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "DW", "SR");
                            DW_DH_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "DW", "DH");
                            DW_DH_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "DW", "DH");
                        }

                        DW_1 = node1.expo[radiusIndex].GetWgtAvg(node1.windRose, WD_Ind, "DW", "Expo");
                        DW_2 = node2.expo[radiusIndex].GetWgtAvg(node2.windRose, WD_Ind, "DW", "Expo");

                        avgDW = (DW_1 + DW_2) / 2;

                        if (thisInst.topo.useSepMod == true) flowSepNodes = nodeList.GetSepNodes1and2(node1.flowSepNodes, node2.flowSepNodes, WD_Ind);

                        coeffsDelta = Get_DeltaWS_UW_Expo(UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, radiusIndex, node1.flowSepNodes, node2.flowSepNodes,
                                                            WS_1, thisInst.topo.useSepMod, nodeUTM1, nodeUTM2);
                        deltaWS_UWExpo = GetTotalWS(coeffsDelta);
                        coeffsDelta = Get_DeltaWS_DW_Expo(WS_1, UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, thisInst.topo.useSepMod);
                        deltaWS_DWExpo = GetTotalWS(coeffsDelta);

                        if (thisInst.topo.gotSR == true) {
                            UW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_1, thisInst.topo.useSepMod, "UW");
                            UW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_2, thisInst.topo.useSepMod, "UW");
                            deltaWS_UW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, UW_SR_1, UW_SR_2, UW_DH_1, UW_DH_2, UW_Stab_Corr_1, UW_Stab_Corr_2);

                            DW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_1, thisInst.topo.useSepMod, "DW");
                            DW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_2, thisInst.topo.useSepMod, "DW");
                            deltaWS_DW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, DW_SR_1, DW_SR_2, DW_DH_1, DW_DH_2, DW_Stab_Corr_1, DW_Stab_Corr_2);
                        }

                        if ((WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR) > 0)
                            WS_Est_Return.sectorWS_AtNodes[nodeInd + 1, WD_Ind] = WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR;
                        else
                            WS_Est_Return.sectorWS_AtNodes[nodeInd + 1, WD_Ind] = 0.05f; // no negative wind speed estimates!

                    }
                    nodeInd++;
                }

                // lastNode to End Node
                thisNode = pathOfNodes[nodeInd];
                nodeUTM1.UTMX = pathOfNodes[nodeInd].UTMX;
                nodeUTM1.UTMY = pathOfNodes[nodeInd].UTMY;

                nodeUTM2.UTMX = endNode.UTMX;
                nodeUTM2.UTMY = endNode.UTMY;

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                    sectorWS[WD_Ind] = WS_Est_Return.sectorWS_AtNodes[nodeInd, WD_Ind];

                WS_Equiv = GetWS_Equiv(thisNode.windRose, endNode.windRose, sectorWS);

                if (thisNode.windRose == null) thisNode.windRose = thisInst.metList.GetInterpolatedWindRose(thisModel.metsUsed, thisNode.UTMX, thisNode.UTMY);

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                {
                    avgP10DW = (endNode.gridStats.stats[radiusIndex].P10_DW[WD_Ind] + thisNode.gridStats.stats[radiusIndex].P10_DW[WD_Ind]) / 2;
                    avgP10UW = (endNode.gridStats.stats[radiusIndex].P10_UW[WD_Ind] + thisNode.gridStats.stats[radiusIndex].P10_UW[WD_Ind]) / 2;

               //     UW_CW_Grade = (thisNode.expo[radiusIndex].UW_P10CrossGrade[WD_Ind] + endNode.expo[radiusIndex].UW_P10CrossGrade[WD_Ind]) / 2;
               //     UW_PL_Grade = (thisNode.expo[radiusIndex].UW_ParallelGrade[WD_Ind] + endNode.expo[radiusIndex].UW_ParallelGrade[WD_Ind]) / 2;

                    UW_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "Expo");
                    UW_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "Expo");

                    avgUW = (UW_1 + UW_2) / 2;

                    WS_1 = WS_Equiv[WD_Ind];

                    if (thisInst.topo.gotSR == true)
                    {
                        UW_SR_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "SR");
                        UW_SR_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "SR");
                        UW_DH_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "UW", "DH");
                        UW_DH_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "DH");

                        DW_SR_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "SR");
                        DW_SR_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "SR");
                        DW_DH_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "DH");
                        DW_DH_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "DH");
                    }

                    DW_1 = thisNode.expo[radiusIndex].GetWgtAvg(thisNode.windRose, WD_Ind, "DW", "Expo");
                    DW_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "Expo");

                    avgDW = (DW_1 + DW_2) / 2;

                    if (thisInst.topo.useSepMod == true) flowSepNodes = nodeList.GetSepNodes1and2(thisNode.flowSepNodes, endNode.flowSepNodes, WD_Ind);

                    coeffsDelta = Get_DeltaWS_UW_Expo(UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, radiusIndex, thisNode.flowSepNodes, endNode.flowSepNodes,
                                                        WS_1, thisInst.topo.useSepMod, nodeUTM1, nodeUTM2);
                    deltaWS_UWExpo = GetTotalWS(coeffsDelta);
                    coeffsDelta = Get_DeltaWS_DW_Expo(WS_1, UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, thisInst.topo.useSepMod);
                    deltaWS_DWExpo = GetTotalWS(coeffsDelta);

                    if (thisInst.topo.gotSR == true)
                    {
                        UW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_1, thisInst.topo.useSepMod, "UW");
                        UW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_2, thisInst.topo.useSepMod, "UW");
                        deltaWS_UW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, UW_SR_1, UW_SR_2, UW_DH_1, UW_DH_2, UW_Stab_Corr_1, UW_Stab_Corr_2);

                        DW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_1, thisInst.topo.useSepMod, "DW");
                        DW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_2, thisInst.topo.useSepMod, "DW");
                        deltaWS_DW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, DW_SR_1, DW_SR_2, DW_DH_1, DW_DH_2, DW_Stab_Corr_1, DW_Stab_Corr_2);
                    }

                    if ((WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR) > 0)
                        WS_Est_Return.sectorWS[WD_Ind] = WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR;
                    else
                        WS_Est_Return.sectorWS[WD_Ind] = 0.05f;

                }
            }
            else {
                // No nodes so just one step from Met 1 to Target
                WS_Equiv = GetWS_Equiv(startMet.windRose, endNode.windRose, sectorWS);
                nodeUTM2.UTMX = endNode.UTMX;
                nodeUTM2.UTMY = endNode.UTMY;

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                {
                    avgP10DW = (startMet.gridStats.stats[radiusIndex].P10_DW[WD_Ind] + endNode.gridStats.stats[radiusIndex].P10_DW[WD_Ind]) / 2;
                    avgP10UW = (startMet.gridStats.stats[radiusIndex].P10_UW[WD_Ind] + endNode.gridStats.stats[radiusIndex].P10_UW[WD_Ind]) / 2;

               //     UW_CW_Grade = (endNode.expo[radiusIndex].UW_P10CrossGrade[WD_Ind] + startMet.expo[radiusIndex].UW_P10CrossGrade[WD_Ind]) / 2;
               //     UW_PL_Grade = (endNode.expo[radiusIndex].UW_ParallelGrade[WD_Ind] + startMet.expo[radiusIndex].UW_ParallelGrade[WD_Ind]) / 2;

                    UW_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "Expo");
                    UW_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "Expo");
                    avgUW = (UW_1 + UW_2) / 2;

                    WS_1 = WS_Equiv[WD_Ind];

                    if (thisInst.topo.gotSR == true) {
                        UW_SR_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "SR");
                        UW_SR_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "SR");
                        UW_DH_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "UW", "DH");
                        UW_DH_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "UW", "DH");

                        DW_SR_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "SR");
                        DW_SR_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "SR");
                        DW_DH_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "DH");
                        DW_DH_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "DH");
                    }

                    DW_1 = startMet.expo[radiusIndex].GetWgtAvg(startMet.windRose, WD_Ind, "DW", "Expo");
                    DW_2 = endNode.expo[radiusIndex].GetWgtAvg(endNode.windRose, WD_Ind, "DW", "Expo");

                    avgDW = (DW_1 + DW_2) / 2;

                    if (thisInst.topo.useSepMod == true) flowSepNodes = nodeList.GetSepNodes1and2(startMet.flowSepNodes, endNode.flowSepNodes, WD_Ind);

                    coeffsDelta = Get_DeltaWS_UW_Expo(UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, radiusIndex, startMet.flowSepNodes, endNode.flowSepNodes, 
                                                        WS_1, thisInst.topo.useSepMod, nodeUTM1, nodeUTM2);
                    deltaWS_UWExpo = GetTotalWS(coeffsDelta);
                    coeffsDelta = Get_DeltaWS_DW_Expo(WS_1, UW_1, UW_2, DW_1, DW_2, avgP10UW, avgP10DW, thisModel, WD_Ind, thisInst.topo.useSepMod);
                    deltaWS_DWExpo = GetTotalWS(coeffsDelta);

                    if (thisInst.topo.gotSR == true)
                    {
                        UW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_1, thisInst.topo.useSepMod, "UW");
                        UW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, UW_SR_2, thisInst.topo.useSepMod, "UW");
                        deltaWS_UW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, UW_SR_1, UW_SR_2, UW_DH_1, UW_DH_2, UW_Stab_Corr_1, UW_Stab_Corr_2);

                        DW_Stab_Corr_1 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_1, thisInst.topo.useSepMod, "DW");
                        DW_Stab_Corr_2 = thisModel.GetStabilityCorrection(avgUW, avgDW, WD_Ind, DW_SR_2, thisInst.topo.useSepMod, "DW");
                        deltaWS_DW_SR = GetDeltaWS_SRDH(WS_1, startMet.height, DW_SR_1, DW_SR_2, DW_DH_1, DW_DH_2, DW_Stab_Corr_1, DW_Stab_Corr_2);
                    }

                    if ((WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR) > 0)
                        WS_Est_Return.sectorWS[WD_Ind] = WS_1 + deltaWS_UWExpo + deltaWS_DWExpo + deltaWS_UW_SR + deltaWS_DW_SR;
                    else
                        WS_Est_Return.sectorWS[WD_Ind] = 0.05f;

                }

            }

            return WS_Est_Return;

        }

   /*     public void SetMinMaxExposInModel(ref Model[] models, MetPairCollection metPairList, InvestCollection radiiList)
            {
            //  Finds the min and max UW and DW exposures in each WD sector and saves to models
            double[,] minMax = new double[4, 2];
            int numWD = 0;

            try {
                numWD = models[0].downhill_A.Length;
            }
            catch  { 
                return;
            }

            int numRadii = radiiList.ThisCount;

            for (int radiusIndex = 0; radiusIndex < numRadii; radiusIndex++)
            { 
                if (models[radiusIndex].minP10ExpoPosDW == null) models[radiusIndex].minP10ExpoPosDW = new double[numWD];
                if (models[radiusIndex].minP10ExpoNegDW == null) models[radiusIndex].minP10ExpoNegDW = new double[numWD];
                if (models[radiusIndex].minP10ExpoPosUW == null) models[radiusIndex].minP10ExpoPosUW = new double[numWD];
                if (models[radiusIndex].minP10ExpoNegUW == null) models[radiusIndex].minP10ExpoNegUW = new double[numWD];

                if (models[radiusIndex].maxP10ExpoPosDW == null) models[radiusIndex].maxP10ExpoPosDW = new double[numWD];
                if (models[radiusIndex].maxP10ExpoNegDW == null) models[radiusIndex].maxP10ExpoNegDW = new double[numWD];
                if (models[radiusIndex].maxP10ExpoPosUW == null) models[radiusIndex].maxP10ExpoPosUW = new double[numWD];
                if (models[radiusIndex].maxP10ExpoNegUW == null) models[radiusIndex].maxP10ExpoNegUW = new double[numWD];                               

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                {
                    minMax = metPairList.GetMinAndMaxP10Expo(models[radiusIndex], radiiList, WD_Ind, numWD);
                    // i = 0 Pos DW, 1 Neg DW, 2 Pos UW, 3 Neg UW; j = 0 Min, 1 Max
                    models[radiusIndex].minP10ExpoPosDW[WD_Ind] = minMax[0, 0];
                    models[radiusIndex].maxP10ExpoPosDW[WD_Ind] = minMax[0, 1];
                    models[radiusIndex].minP10ExpoNegDW[WD_Ind] = minMax[1, 0];
                    models[radiusIndex].maxP10ExpoNegDW[WD_Ind] = minMax[1, 1];
                    models[radiusIndex].minP10ExpoPosUW[WD_Ind] = minMax[2, 0];
                    models[radiusIndex].maxP10ExpoPosUW[WD_Ind] = minMax[2, 1];
                    models[radiusIndex].minP10ExpoNegUW[WD_Ind] = minMax[3, 0];
                    models[radiusIndex].maxP10ExpoNegUW[WD_Ind] = minMax[3, 1];
                }
            }

        }
        */
        public void FindModelBounds(ref Model[] models, MetPairCollection metPairList, InvestCollection radiiList)
        {
            //  Finds the model bounds (i.e. min/max P10 exposure) for each radius, WD, and flow type. Saves the met site with min/max P10 expo for each model type.  
            
            int numWD = 0;

            try
            {
                numWD = models[0].downhill_A.Length;
            }
            catch
            {
                return;
            }

            int numRadii = radiiList.ThisCount;
            Met thisMet;
            string flowType;
            
            for (int radiusIndex = 0; radiusIndex < numRadii; radiusIndex++)
            {               

                for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                {
                    for (int pairInd = 0; pairInd < metPairList.PairCount; pairInd++)
                    {
                        for (int i = 0; i <= 1; i++)
                        {
                            if (i == 0)
                                thisMet = metPairList.metPairs[pairInd].met1;
                            else
                                thisMet = metPairList.metPairs[pairInd].met2;

                            double thisP10 = Math.Max(thisMet.gridStats.stats[radiusIndex].P10_UW[WD_Ind], thisMet.gridStats.stats[radiusIndex].P10_DW[WD_Ind]);

                            for (int j = 0; j <= 1; j++)
                            {
                                // Get upwind flow type (i.e. downhill, uphill, or speed-up) at Met
                                if (j == 0)
                                    flowType = models[radiusIndex].GetFlowType(thisMet.expo[radiusIndex].expo[WD_Ind], thisMet.expo[radiusIndex].GetDW_Param(WD_Ind, "Expo"),
                                        WD_Ind, "UW", null, 0, false, 0);
                                else
                                    flowType = models[radiusIndex].GetFlowType(thisMet.expo[radiusIndex].expo[WD_Ind], thisMet.expo[radiusIndex].GetDW_Param(WD_Ind, "Expo"),
                                            WD_Ind, "DW", null, 0, false, 0);
                                
                                if (flowType == "Downhill")
                                {
                                    if (thisP10 < models[radiusIndex].downhillBounds[WD_Ind].min || models[radiusIndex].downhillBounds[WD_Ind].min == -999)
                                    {
                                        models[radiusIndex].downhillBounds[WD_Ind].min = thisP10;
                                        models[radiusIndex].downhillBounds[WD_Ind].metMinP10 = thisMet;
                                    }
                                    else if (thisP10 > models[radiusIndex].downhillBounds[WD_Ind].max || models[radiusIndex].downhillBounds[WD_Ind].max == -999)
                                    {
                                        models[radiusIndex].downhillBounds[WD_Ind].max = thisP10;
                                        models[radiusIndex].downhillBounds[WD_Ind].metMaxP10 = thisMet;
                                    }
                                }
                                else if (flowType == "Uphill")
                                {
                                    if (thisP10 < models[radiusIndex].uphillBounds[WD_Ind].min || models[radiusIndex].uphillBounds[WD_Ind].min == -999)
                                    {
                                        models[radiusIndex].uphillBounds[WD_Ind].min = thisP10;
                                        models[radiusIndex].uphillBounds[WD_Ind].metMinP10 = thisMet;
                                    }
                                    else if (thisP10 > models[radiusIndex].uphillBounds[WD_Ind].max || models[radiusIndex].uphillBounds[WD_Ind].max == -999)
                                    {
                                        models[radiusIndex].uphillBounds[WD_Ind].max = thisP10;
                                        models[radiusIndex].uphillBounds[WD_Ind].metMaxP10 = thisMet;
                                    }
                                }
                                else if (flowType == "SpdUp")
                                {
                                    if (thisP10 < models[radiusIndex].spdUpBounds[WD_Ind].min || models[radiusIndex].spdUpBounds[WD_Ind].min == -999)
                                    {
                                        models[radiusIndex].spdUpBounds[WD_Ind].min = thisP10;
                                        models[radiusIndex].spdUpBounds[WD_Ind].metMinP10 = thisMet;
                                    }
                                    else if (thisP10 > models[radiusIndex].spdUpBounds[WD_Ind].max || models[radiusIndex].spdUpBounds[WD_Ind].max == -999)
                                    {
                                        models[radiusIndex].spdUpBounds[WD_Ind].max = thisP10;
                                        models[radiusIndex].spdUpBounds[WD_Ind].metMaxP10 = thisMet;
                                    }
                                }
                            }
                        }
                    }
                    
                }
            }

        }

        public void CalcRMS_Overall_and_Sectorwise(ref Model[] models, Continuum thisInst)
        {
            // Finds the RMS of all met cross-prediction error (overall and sectorwise). Only using met pairs where both mets are used in model
            
            int numRadii = models.Length;
            int numWD = models[0].downhill_A.Length;
            double RMS_WS_Est = 0;
            double RMS_WS_Est_count = 0;
            double[] RMS_Sect_WS = new double[numWD];
            double[] RMS_Sect_WS_count = new double[numWD];                                                      
            int pairCount = thisInst.metPairList.PairCount;
            
            if (pairCount > 0)
            {
                for (int radiusIndex = 0; radiusIndex < numRadii; radiusIndex++)
                {
                    Model model = models[radiusIndex];                                                                         

                    for (int j = 0; j < pairCount; j++)
                    {
                        if (model.IsMetUsedInModel(thisInst.metPairList.metPairs[j].met1.name) && model.IsMetUsedInModel(thisInst.metPairList.metPairs[j].met2.name))
                        {
                            int WS_PredInd = GetWS_PredInd(thisInst.metPairList.metPairs[j], models);

                            if (WS_PredInd == -1)
                                return;

                            RMS_WS_Est = RMS_WS_Est + Math.Pow(thisInst.metPairList.metPairs[j].WS_Pred[WS_PredInd, radiusIndex].percErr[1], 2);
                            RMS_WS_Est_count++;

                            for (int WD = 0; WD < numWD; WD++)
                            {
                                RMS_Sect_WS[WD] = RMS_Sect_WS[WD] + Math.Pow(thisInst.metPairList.metPairs[j].WS_Pred[WS_PredInd, radiusIndex].percErrSector[1, WD], 2);
                                RMS_Sect_WS_count[WD]++;
                            }

                            RMS_WS_Est = RMS_WS_Est + Math.Pow(thisInst.metPairList.metPairs[j].WS_Pred[WS_PredInd, radiusIndex].percErr[0], 2);
                            RMS_WS_Est_count++;

                            for (int WD = 0; WD < numWD; WD++)
                            {
                                RMS_Sect_WS[WD] = RMS_Sect_WS[WD] + Math.Pow(thisInst.metPairList.metPairs[j].WS_Pred[WS_PredInd, radiusIndex].percErrSector[0, WD], 2);
                                RMS_Sect_WS_count[WD]++;
                            }
                        }
                    }

                    if (RMS_WS_Est_count > 0)
                        RMS_WS_Est = Math.Pow((RMS_WS_Est / RMS_WS_Est_count), 0.5);
                    else
                        RMS_WS_Est = 0;
                    

                    for (int WD = 0; WD < numWD; WD++)
                    {
                        if (RMS_Sect_WS_count[WD] > 0)
                            RMS_Sect_WS[WD] = Math.Pow((RMS_Sect_WS[WD] / RMS_Sect_WS_count[WD]), 0.5);
                        else
                            RMS_Sect_WS[WD] = 0;
                        
                    }

                    models[radiusIndex].RMS_WS_Est = RMS_WS_Est;
                    models[radiusIndex].RMS_Sect_WS_Est = RMS_Sect_WS;

                    RMS_WS_Est = 0;
                    RMS_WS_Est_count = 0;

                    RMS_Sect_WS = new double[numWD];
                    RMS_Sect_WS_count = new double[numWD];
                }
            }
        }

        public int GetWS_PredInd(Pair_Of_Mets metPair, Model[] model)
        {
            // Finds and returns the index of model in metPair//s WS_Estimates
            int WS_PredInd = -1;
            bool isSame = false;

            for (int i = 0; i < metPair.WS_PredCount; i++)
            {
                isSame = IsSameModel(model[0], metPair.WS_Pred[i, 0].model); // just comparing with first radii to find WS_PredInd
                if (isSame == true)
                {
                    WS_PredInd = i;
                    break;
                }
            }

            return WS_PredInd;
        }

        public Coeff_Delta_WS[] Get_DeltaWS_DW_Expo(double WS1, double UW1, double UW2, double DW1, double DW2, double P10_UW, double P10_DW, Model thisModel, int WD_Ind, bool useSepModel)
        {
            // Returns the coefficients and change in wind speed for each flow type bed on change in either UW or DW exposure
            Coeff_Delta_WS[] coeffsDelta = null;
            double deltaDH = 0;
            double deltaUH = 0;
            double deltaFS = 0;
            double deltaWS = 0;
            
            double coeff = 0;

            InvestCollection radiiList = new InvestCollection();
            radiiList.New();
            int radiusIndex = 0;
            for (int i = 0; i < radiiList.ThisCount; i++)
                if (radiiList.investItem[i].radius == thisModel.radius)                
                    radiusIndex = i;                    
                
            string flow1 = thisModel.GetFlowType(UW1, DW1, WD_Ind, "DW", null, WS1, useSepModel, radiusIndex);
            string flow2 = thisModel.GetFlowType(UW2, DW2, WD_Ind, "DW", null, WS1, useSepModel, radiusIndex);

            double avgUW = (UW1 + UW2) / 2;
            double avgDW = (DW1 + DW2) / 2;            

       /*     if (useSepModel == true && DW1 > 0 && UW1 > 0 && DW1 + UW1 > thisModel.sepCrit[WD_Ind] && WS1 > thisModel.Sep_crit_WS[WD_Ind])
                flow1 = "Turbulent";
            else if (DW1 > 0)
                flow1 = "Downhill";
            else
                flow1 = "Uphill";

            if (useSepModel = true && DW2 > 0 && UW2 > 0 && DW2 + UW2 > thisModel.sepCrit[WD_Ind] && WS1 > thisModel.Sep_crit_WS[WD_Ind])
                flow2 = "Turbulent";
            else if (DW2 > 0)
                flow2 = "Downhill";
            else
                flow2 = "Uphill";
                */

            if (flow1 == flow2)
            {
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaWS = coeff * (DW2 - DW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, flow1, "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "Uphill")
            {
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaDH = coeff * (0 - DW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaUH = coeff * (DW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");


                deltaWS = deltaDH + deltaUH;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "Downhill")
            {
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaUH = coeff * (0 - DW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaDH = coeff * (DW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");

                deltaWS = deltaUH + deltaDH;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "Turbulent")
            {
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaDH = coeff * ((thisModel.sepCrit[WD_Ind] - avgUW) - DW1);
                if (deltaDH < 0) deltaDH = 0; // WS should increase between DW1 and point of separation
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaFS = coeff * (DW2 - (thisModel.sepCrit[WD_Ind] - avgUW));
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaFS, flow2, "Expo");
                if (deltaFS > 0) deltaFS = 0; // WS should decrease between point of separation and turbulent site 2

                deltaWS = deltaDH + deltaFS;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "Downhill")
            {
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaFS = coeff * ((thisModel.sepCrit[WD_Ind] - avgUW) - DW1);
                if (deltaFS < 0) deltaFS = 0; // WS should increase between turbulent site 1 and point of separation 
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaFS, flow1, "Expo");

                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaDH = coeff * (DW2 - (thisModel.sepCrit[WD_Ind] - avgUW));
                if (deltaDH > 0) deltaDH = 0; // WS should decrease between point of separation and DW2 
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");

                deltaWS = deltaFS + deltaDH;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "Turbulent")
            {
                // Uphill to flat
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaUH = coeff * (0 - DW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                // Flat to POS
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, "Downhill");
                deltaDH = coeff * ((thisModel.sepCrit[WD_Ind] - avgUW) - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, "Downhill", "Expo");

                // POS to Site 2
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaFS = coeff * (DW2 - (thisModel.sepCrit[WD_Ind] - avgUW));
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaFS, flow2, "Expo"); // WS should decrease... it doesn't always, this needs to be looked into 

                deltaWS = deltaUH + deltaDH + deltaFS;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "Uphill")
            {
                // Turbulent to point of separation
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow1);
                deltaFS = coeff * ((thisModel.sepCrit[WD_Ind] - avgUW) - DW1);
                if (deltaFS < 0) deltaFS = 0; // WS should incree between turbulent site 1 and point of separation 
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaFS, flow1, "Expo");

                // POS to Flat
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, "Downhill");
                deltaDH = coeff * (0 - (thisModel.sepCrit[WD_Ind] - avgUW));
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, "Downhill", "Expo");

                // Flat to Uphill
                coeff = thisModel.CalcDW_Coeff(P10_DW, P10_UW, WD_Ind, flow2);
                deltaUH = coeff * (DW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");

                deltaWS = deltaFS + deltaDH + deltaUH;
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Total", "Expo");
            }

            return coeffsDelta;
        }

        public void AddCoeffDeltaWS(ref Coeff_Delta_WS[] coeffsDelta, double coeff, double deltaWS, string flowType, string Expo_or_SRDH)
        {
            //  Adds coefficients and delta WS to list of coeffsDelta
            int numCoeffDelt = 0;
            if (coeffsDelta != null)
                numCoeffDelt = coeffsDelta.Length;

            Array.Resize(ref coeffsDelta, numCoeffDelt + 1);

            coeffsDelta[numCoeffDelt].coeff = coeff;
            coeffsDelta[numCoeffDelt].deltaWS_Expo = deltaWS;
            coeffsDelta[numCoeffDelt].expoOrRough = Expo_or_SRDH;
            coeffsDelta[numCoeffDelt].flowType = flowType;

        }

        public NodeCollection.Sep_Nodes GetFlowSepNodes(NodeCollection.Sep_Nodes[] flowSepNodes, int WD_Ind)
        {
            // Returns flow separation nodes for specified WD sector if they exist
            NodeCollection.Sep_Nodes thisFlowSep = new NodeCollection.Sep_Nodes();
            int numFS = 0;

            if (flowSepNodes != null) numFS = flowSepNodes.Length;
            
            for (int i = 0; i < numFS; i++)
            {
                if (flowSepNodes[i].flowSepWD == WD_Ind)
                {
                    thisFlowSep = flowSepNodes[i];
                    break;
                }
            }

            return thisFlowSep;
        }

        public Coeff_Delta_WS[] GetDeltaWS_UW_Turbulent(NodeCollection.Node_UTMs siteCoords, double siteUWExpo, NodeCollection.Sep_Nodes flowSepNodes, Model thisModel, 
                bool fromSiteToSepNode, int WD_Ind, int radiusIndex, double P10_UW, double P10_DW)
        {
            Coeff_Delta_WS[] coeffsDelta = null;
            TopoInfo topo = new TopoInfo();
            double deltaFS = 0;

            if (fromSiteToSepNode == true) // predicting change in wind speed from site to upwind point of separation
            {
                if (flowSepNodes.turbEndNode == null)
                { // Site 1 is in turb zone so go to Sep Pt 1                                                               
                  // Site 1 to Sep Pt 1
                    double distToHigh = topo.CalcDistanceBetweenPoints(flowSepNodes.highNode.UTMX, flowSepNodes.highNode.UTMY, siteCoords.UTMX, siteCoords.UTMY);
                    deltaFS = -thisModel.GetDeltaWS_TurbulentZone(distToHigh, WD_Ind);
                    AddCoeffDeltaWS(ref coeffsDelta, distToHigh, deltaFS, "Turbulent", "Expo");
                }
                else
                { // Site 1 is outside of turb so go to end of turb zone then go to Sep Pt 1
                  // Site 1 to Turb End 1
                    double coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Downhill");
                    double deltaDH = coeff * (flowSepNodes.turbEndNode.expo[radiusIndex].expo[WD_Ind] - siteUWExpo);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, "Downhill", "Expo");

                    // Turb End 1 to Sep Pt 1
                    double distToHigh = topo.CalcDistanceBetweenPoints(flowSepNodes.highNode.UTMX, flowSepNodes.highNode.UTMY, flowSepNodes.turbEndNode.UTMX, flowSepNodes.turbEndNode.UTMY);
                    deltaFS = -thisModel.GetDeltaWS_TurbulentZone(distToHigh, WD_Ind);
                    AddCoeffDeltaWS(ref coeffsDelta, distToHigh, deltaFS, "Turbulent", "Expo");
                }
            }
            else // predicting change in wind speed from upwind point of separation to site
            {
                if (flowSepNodes.turbEndNode == null)
                { // Site 2 is in turb zone so go to Site 2                                                                
                  // Sep Pt 2 to Site 2
                    double distToHigh = topo.CalcDistanceBetweenPoints(siteCoords.UTMX, siteCoords.UTMY, flowSepNodes.highNode.UTMX, flowSepNodes.highNode.UTMY);
                    double deltaWS = thisModel.GetDeltaWS_TurbulentZone(distToHigh, WD_Ind);
                    AddCoeffDeltaWS(ref coeffsDelta, distToHigh, deltaWS, "Turbulent", "Expo");                    
                }
                else
                { // Sep Pt 2 to Turb End 2
                    double distToHigh = topo.CalcDistanceBetweenPoints(flowSepNodes.turbEndNode.UTMX, flowSepNodes.turbEndNode.UTMY, flowSepNodes.highNode.UTMX, flowSepNodes.highNode.UTMY);
                    double deltaWS = thisModel.GetDeltaWS_TurbulentZone(distToHigh, WD_Ind);
                    AddCoeffDeltaWS(ref coeffsDelta, distToHigh, deltaWS, "Turbulent", "Expo");
                    
                    // Turb End 2 to Site 2
                    double coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Downhill");
                    deltaWS = coeff * (siteUWExpo - flowSepNodes.turbEndNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Downhill", "Expo");                    
                }
            }

            return coeffsDelta;
        }

        public Coeff_Delta_WS[] Get_DeltaWS_UW_Expo(double UW1, double UW2, double DW1, double DW2, double P10_UW, double P10_DW, Model thisModel, int WD_Ind, int radiusIndex, NodeCollection.Sep_Nodes[] sepNodes1,
                                            NodeCollection.Sep_Nodes[] sepNodes2, double WS, bool useFlowSep, NodeCollection.Node_UTMs node1Coords, NodeCollection.Node_UTMs node2Coords)
        
        { 
            // Returns the coeffs and change in wind speed for each flow type bed on change in either UW or DW exposure or SRDH or Turbulence
            Coeff_Delta_WS[] coeffsDelta = null;
            double deltaDH = 0;
            double deltaSU = 0;
            double deltaUH = 0;            
            double deltaVL = 0;
            double deltaWS = 0;

            double coeff = 0;               
                        
            TopoInfo topo = new TopoInfo();

            NodeCollection.Sep_Nodes flowSepNode1 = GetFlowSepNodes(sepNodes1, WD_Ind);
            NodeCollection.Sep_Nodes flowSepNode2 = GetFlowSepNodes(sepNodes2, WD_Ind); ;

            string flow1 = thisModel.GetFlowType(UW1, DW1, WD_Ind, "UW", sepNodes1, WS, useFlowSep, radiusIndex);
            string flow2 = thisModel.GetFlowType(UW2, DW2, WD_Ind, "UW", sepNodes2, WS, useFlowSep, radiusIndex);

            if (flow1 == "Turbulent" && flow2 == "Turbulent")
            { // Scenario 1             
                
                coeffsDelta = GetDeltaWS_UW_Turbulent(node1Coords, UW1, flowSepNode1, thisModel, true, WD_Ind, radiusIndex, P10_UW, P10_DW); // Site 1 to Sep Pt 1
           
                // Sep Pt 1 to Sep Pt 2 // for now sume that flow is uphill
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                deltaUH = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                Coeff_Delta_WS[] turbCoeffDeltaWS = GetDeltaWS_UW_Turbulent(node2Coords, UW2, flowSepNode2, thisModel, false, WD_Ind, radiusIndex, P10_UW, P10_DW); // Sep Pt 2 to Site 2
                int coeffsDeltOldLength = coeffsDelta.Length;
                Array.Resize(ref coeffsDelta, coeffsDeltOldLength + turbCoeffDeltaWS.Length);
                Array.Copy(turbCoeffDeltaWS, 0, coeffsDelta, coeffsDeltOldLength, turbCoeffDeltaWS.Length);                              

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
               
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == flow2) {  // Scenario 2

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaWS = coeff * (UW2 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, flow1, "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "Uphill")
            { // Scenario 3
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaDH = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaUH = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");

                deltaWS = deltaDH + deltaSU + deltaUH;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "SpdUp")
            { // Scenario 4
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaDH = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaSU = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow2, "Expo");

                deltaWS = deltaDH + deltaSU;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "Valley")
            { // Scenario 5// delta WS must be< 0. Can't accelerate if going from downhill flow into a valley.
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaVL = coeff * (UW2 - UW1);

                if (deltaVL < 0)
                    deltaWS = deltaVL;
                else
                    deltaWS = 0;

                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaWS, "Valley", "Expo");
            }
            else if (flow1 == "Downhill" && flow2 == "Turbulent")
            { // Scenario 6
                flowSepNode2 = GetFlowSepNodes(sepNodes2, WD_Ind);

                // Site 1 to Sep Pt 2
                if (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind])
                { // Sep Pt 2 is Uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaDH = coeff * (0 - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");
                }
                else { // Sep Pt 2 is Speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaDH = coeff * (0 - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");
                }

                // Sep Pt 2 to Site 2
                Coeff_Delta_WS[] Coeffs_Delta_Turb = GetDeltaWS_UW_Turbulent(node2Coords, UW2, flowSepNode2, thisModel, false, WD_Ind, radiusIndex, P10_UW, P10_DW);
                int coeffsDeltOldLength = coeffsDelta.Length;
                Array.Resize(ref coeffsDelta, coeffsDeltOldLength + Coeffs_Delta_Turb.Length);
                Array.Copy(Coeffs_Delta_Turb, 0, coeffsDelta, coeffsDeltOldLength, Coeffs_Delta_Turb.Length);

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;

                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "Downhill")
            { // Scenario 7
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                deltaSU = coeff * (0 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaDH = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");

                deltaWS = deltaUH + deltaSU + deltaDH;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "SpdUp")
            { // Scenario 8
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaSU = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow2, "Expo");

                deltaWS = deltaUH + deltaSU;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "Valley")
            { // Scenario 9// 
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                deltaSU = coeff * (0 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                // Flat - Valley
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaVL = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow2, "Expo");

                deltaWS = deltaUH + deltaSU + deltaVL;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Uphill" && flow2 == "Turbulent")
            { // Scenario 10
                flowSepNode2 = GetFlowSepNodes(sepNodes2, WD_Ind);

                // Site 1 to Sep Pt 2
                if (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind])
                { // Sep Pt 2 is Uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaUH = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");
                }
                else { // Sep Pt 2 is Speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");
                }

                // Sep Pt 2 to Site 2
                Coeff_Delta_WS[] Coeffs_Delta_Turb = GetDeltaWS_UW_Turbulent(node2Coords, UW2, flowSepNode2, thisModel, false, WD_Ind, radiusIndex, P10_UW, P10_DW);
                int coeffsDeltOldLength = coeffsDelta.Length;
                Array.Resize(ref coeffsDelta, coeffsDeltOldLength + Coeffs_Delta_Turb.Length);
                Array.Copy(Coeffs_Delta_Turb, 0, coeffsDelta, coeffsDeltOldLength, Coeffs_Delta_Turb.Length);

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "SpdUp" && flow2 == "Uphill")
            { // Scenario 11
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaUH = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");

                deltaWS = deltaSU + deltaUH;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "SpdUp" && flow2 == "Downhill")
            { // Scenario 12
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaSU = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaDH = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");

                deltaWS = deltaSU + deltaDH;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "SpdUp" && flow2 == "Valley")
            { // Scenario 13
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaSU = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaVL = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow2, "Expo");

                deltaWS = deltaSU + deltaVL;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "SpdUp" && flow2 == "Turbulent")
            { // Scenario 14
                flowSepNode2 = GetFlowSepNodes(sepNodes2, WD_Ind);

                // Site 1 to Sep Pt 2
                if (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] < thisModel.UW_crit[WD_Ind])
                { // Sep Pt 2 is Speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaSU = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow1, "Expo");
                }
                else { // Sep Pt 2 is Uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");
                }

                // Sep Pt 2 to Site 2
                Coeff_Delta_WS[] Coeffs_Delta_Turb = GetDeltaWS_UW_Turbulent(node2Coords, UW2, flowSepNode2, thisModel, false, WD_Ind, radiusIndex, P10_UW, P10_DW);
                int coeffsDeltOldLength = coeffsDelta.Length;
                Array.Resize(ref coeffsDelta, coeffsDeltOldLength + Coeffs_Delta_Turb.Length);
                Array.Copy(Coeffs_Delta_Turb, 0, coeffsDelta, coeffsDeltOldLength, Coeffs_Delta_Turb.Length);

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                                
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Valley" && flow2 == "Uphill")
            { // Scenario 15                                                               
                // Valley - Flat 
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaVL = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaUH = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");

                deltaWS = deltaUH + deltaSU + deltaVL;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Valley" && flow2 == "Downhill")
            { // Scenario 16
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaDH = coeff * (UW2 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow1, "Expo");

                deltaWS = deltaDH;
            }
            else if (flow1 == "Valley" && flow2 == "SpdUp")
            { // Scenario 17                                                              
                // Valley - Flat 
                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                deltaVL = coeff * (0 - UW1);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow1, "Expo");

                coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                deltaSU = coeff * (UW2 - 0);
                AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow2, "Expo");

                deltaWS = deltaSU + deltaVL;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Valley" && flow2 == "Turbulent")
            { // Scenario 18
                flowSepNode2 = GetFlowSepNodes(sepNodes2, WD_Ind);
                // Site 1 to Sep Pt 2
                if (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind])
                { // Sep Pt 2 is Uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaVL = coeff * (0 - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");
                }
                else { // Sep Pt 2 is Speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow1);
                    deltaVL = coeff * (0 - UW1);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow1, "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (flowSepNode2.highNode.expo[radiusIndex].expo[WD_Ind] - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");
                }

                // Sep Pt 2 to Site 2
                Coeff_Delta_WS[] Coeffs_Delta_Turb = GetDeltaWS_UW_Turbulent(node2Coords, UW2, flowSepNode2, thisModel, false, WD_Ind, radiusIndex, P10_UW, P10_DW);
                int coeffsDeltOldLength = coeffsDelta.Length;
                Array.Resize(ref coeffsDelta, coeffsDeltOldLength + Coeffs_Delta_Turb.Length);
                Array.Copy(Coeffs_Delta_Turb, 0, coeffsDelta, coeffsDeltOldLength, Coeffs_Delta_Turb.Length);

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "Uphill")
            { // Scenario 19
                flowSepNode1 = GetFlowSepNodes(sepNodes1, WD_Ind);
                // Site 1 to Sep Pt 1
                coeffsDelta = GetDeltaWS_UW_Turbulent(node1Coords, UW1, flowSepNode1, thisModel, true, WD_Ind, radiusIndex, P10_UW, P10_DW);                            

                if (flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind]) // Sep Pt 1 to Site 2
                { // Sep Pt 1 is uphill

                    // Sep Pt 1 to Site 2
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaUH = coeff * (UW2 - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");
                }
                else { // Sep Pt 1 is speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (thisModel.UW_crit[WD_Ind] - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);

                    deltaUH = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, flow2, "Expo");

                }

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "Downhill")
            { // Scenario 20
                flowSepNode1 = GetFlowSepNodes(sepNodes1, WD_Ind);

                // Site 1 to Sep Pt 1
                coeffsDelta = GetDeltaWS_UW_Turbulent(node1Coords, UW1, flowSepNode1, thisModel, true, WD_Ind, radiusIndex, P10_UW, P10_DW);                               

                if (flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind])
                { // Sep Pt 1 is uphill

                    // Sep Pt 1 to Site 2
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (0 - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaDH = deltaDH + coeff * (UW2 - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");
                }
                else
                { // Sep Pt 1 is speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (0 - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaDH = coeff * (UW2 - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaDH, flow2, "Expo");                    
                }

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "SpdUp")
            { // Scenario 21
                flowSepNode1 = GetFlowSepNodes(sepNodes1, WD_Ind);

                // Site 1 to Sep Pt 1
                coeffsDelta = GetDeltaWS_UW_Turbulent(node1Coords, UW1, flowSepNode1, thisModel, true, WD_Ind, radiusIndex, P10_UW, P10_DW);                               

                if (flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind] < thisModel.UW_crit[WD_Ind])
                { // Sep Pt 1 is speed-up, Sep Pt 1 to Site 2
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaSU = coeff * (UW2 - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow2, "Expo");
                }
                else
                { // Sep Pt 1 is uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaSU = coeff * (UW2 - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, flow2, "Expo");
                }

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");
            }
            else if (flow1 == "Turbulent" && flow2 == "Valley")
            { // Scenario 22
                flowSepNode1 = GetFlowSepNodes(sepNodes1, WD_Ind);
                // Site 1 to Sep Pt 1
                coeffsDelta = GetDeltaWS_UW_Turbulent(node1Coords, UW1, flowSepNode1, thisModel, true, WD_Ind, radiusIndex, P10_UW, P10_DW);                              

                // Sep Pt 1 to Site 2 
                if (flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind] > thisModel.UW_crit[WD_Ind])
                { // Sep Pt 2 is Uphill
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "Uphill");
                    deltaUH = coeff * (thisModel.UW_crit[WD_Ind] - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaUH, "Uphill", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (0 - thisModel.UW_crit[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaVL = coeff * (UW2 - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow2, "Expo");
                }
                else { // Sep Pt 2 is Speed-up
                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, "SpdUp");
                    deltaSU = coeff * (0 - flowSepNode1.highNode.expo[radiusIndex].expo[WD_Ind]);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaSU, "SpdUp", "Expo");

                    coeff = thisModel.CalcUW_Coeff(P10_UW, P10_DW, WD_Ind, flow2);
                    deltaVL = coeff * (UW2 - 0);
                    AddCoeffDeltaWS(ref coeffsDelta, coeff, deltaVL, flow2, "Expo");
                }

                for (int i = 0; i < coeffsDelta.Length; i++)
                    deltaWS = deltaWS + coeffsDelta[i].deltaWS_Expo;
                AddCoeffDeltaWS(ref coeffsDelta, 0, deltaWS, "Total", "Expo");

            }

            return coeffsDelta;
        }

        public double GetDeltaWS_SRDH(double WS1, double HH, double SR1, double SR2, double DH1, double DH2, double Stab_1, double Stab_2)
        {

            // Returns the change in WS bed on change in either UW or DW surface roughness and displacement height
            double deltaWS = 0;

            if (SR1 > 0 && SR2 > 0) 
                deltaWS = WS1 * ((Math.Log((HH - DH2) / SR2) + Stab_2) / (Math.Log((HH - DH1) / SR1) + Stab_1)) - WS1;

            return deltaWS;

        }

        public double[] GetWS_Equiv(double[] WR_Pred, double[] WR_Targ, double[] WS_Pred)
        {
            // Flow Rotation Model 4/20/16
            // Calculates the weighted average WS of predictor met using the target and predictor wind roses
            // Also calculates the change in the wind rose for each sector
            // Adj. factors are defined, one for sectors with positive WR change and one for sectors with negative delta WR with a default value of 0.01 
            // WS_Equiv is calculated for each sector = WS_Pred * Adj. factors * deltaWR
            // Wgt average wind speed is calculated with WS_Equiv. if ( Avg WS < Avg WS Pred then posAdj is increed using 0.02 increments until Avg WS = Avg WS Pred
            // Vice-versa if Avg WS > Avg WS Pred then Neg_adj incree
                        
            double posAdj = 0;
            double negAdj = 0;

            double avgWS_Pred = 0;
            double avgWS_Equiv = 0;
            double WS_Diff = 0;

            int numWD = WR_Pred.Length;
            double[] WS_Equiv = new double[numWD];
            double[] deltaWR = new double[numWD];

            for (int i = 0; i < numWD; i++)
            {
                deltaWR[i] = WR_Targ[i] - WR_Pred[i];

                if (deltaWR[i] >= 0)
                    WS_Equiv[i] = WS_Pred[i] + deltaWR[i] * posAdj;
                else
                    WS_Equiv[i] = WS_Pred[i] + deltaWR[i] * negAdj;

                avgWS_Pred = avgWS_Pred + WS_Pred[i] * WR_Pred[i];
                avgWS_Equiv = avgWS_Equiv + WS_Equiv[i] * WR_Targ[i];
            }

            WS_Diff = avgWS_Equiv - avgWS_Pred;
            double thisMin = 1000;

            while (Math.Abs(WS_Diff) > 0.01 && thisMin > 0.05)
            {
                if (WS_Diff > 0)
                {
                    negAdj = negAdj + Math.Abs(WS_Diff) * 3f;
                    posAdj = posAdj - Math.Abs(WS_Diff) * 1.5f;
                }
                else {
                    negAdj = negAdj - Math.Abs(WS_Diff) * 1.5f;
                    posAdj = posAdj + Math.Abs(WS_Diff) * 3f;
                }

                avgWS_Equiv = 0;

                for (int i = 0; i < numWD; i++)
                {
                    if (deltaWR[i] >= 0)
                        WS_Equiv[i] = WS_Pred[i] + deltaWR[i] * posAdj;
                    else
                        WS_Equiv[i] = WS_Pred[i] + deltaWR[i] * negAdj;

                    if (WS_Equiv[i] < 0) WS_Equiv[i] = 0.05f;
                    avgWS_Equiv = avgWS_Equiv + WS_Equiv[i] * WR_Targ[i];

                    if (WS_Equiv[i] < thisMin) thisMin = WS_Equiv[i];

                }

                WS_Diff = avgWS_Equiv - avgWS_Pred;
            }

            // return WS_Pred // NO FLOW ROTATION
            return WS_Equiv;

        }

        public bool IsWithinModelLimit(Grid_Info Site_1_Stats, double Site_1_Elev, Grid_Info Site_2_Stats, double Site_2_Elev, int radiusIndex, double[] windRose)
        {
            bool Is_In_Limits = true;

            if (Math.Abs(Site_1_Elev - Site_2_Elev) > maxElevAllowed)
                Is_In_Limits = false;
           
            // Check overall expo difference
            double Site1_Overall_P10DW = Site_1_Stats.GetOverallP10(windRose, radiusIndex, "DW");
            double Site1_Overall_P10UW = Site_1_Stats.GetOverallP10(windRose, radiusIndex, "UW");
            double Site2_Overall_P10DW = Site_2_Stats.GetOverallP10(windRose, radiusIndex, "DW");
            double Site2_Overall_P10UW = Site_2_Stats.GetOverallP10(windRose, radiusIndex, "UW");

            if (Math.Abs(Site1_Overall_P10DW - Site2_Overall_P10DW) > maxP10ExpoAllowed || Math.Abs(Site1_Overall_P10UW - Site2_Overall_P10UW) > maxP10ExpoAllowed)
                Is_In_Limits = false;

            return Is_In_Limits;
        }

    }

}