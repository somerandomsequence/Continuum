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
    public class MetCollection
    {
        public Met[] metItem;
        public bool expoIsCalc;
        public bool SRDH_IsCalc;
        public bool isTimeSeries;
        public bool filteringEnabled;
        public bool isMCPd; // True if MERRA2 data is uploaded and MCP is conducted
        public bool allMCPd; // True if MCP has been conducted at all met sites (time series only). Models and estimates are not created unless this is true
        public double WS_FirstInt; // Upper bound of first wind speed bin (to be consistent with TAB file format)
        public double WS_IntSize; // Wind speed bin size (moved this from Met object) To Do: If importing a met TAB file, and it's the first one and it has a different first WS or different bin size, this should be updated
        public int numWD;
        public int numWS;
        public int numTOD;
        public int numSeason;
        public double mcpWS_BinWidth;
        public double mcpMatrixWgt;
        public double mcpLastWS_Wgt;
        public int mcpUncertStepSize; // MCP uncertainty analysis step size (in months)

        public int dayStartHour = 7;
        public int dayEndHour = 18;

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Defined Type: Header_Details. Used to hold information contained in the raw data file headers
        /// including the type of sensor, the sensor ID and the type of measurement (i.e. average, min,
        /// max or standard deviation)
        /// </summary>
        ///
        /// <remarks>   Liz, 6/21/2017. </remarks>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public struct Header_Details
        {
            /// <summary>   Type of sensor (Anem, Vane or temp). </summary>
            public String sensorType;
            /// <summary>   The height (in meters). </summary>
            public int height;
            /// <summary>   The sensor ID (A or B if there are redundant sensors). </summary>
            public Char ID;
            /// <summary>   The boom orientation. </summary>
            public int orient;
            /// <summary>   Type of the measurement (average, min, max or SD). </summary>
            public String measType;
        }

        public void NewList()
        {
            mcpWS_BinWidth = 1;
            mcpMatrixWgt = 1;
            mcpLastWS_Wgt = 1;
            mcpUncertStepSize = 1;

            dayStartHour = 7;
            dayEndHour = 18;

            numTOD = 1;
            numSeason = 1;
            numWD = 16;
            numWS = 30;

            WS_FirstInt = 0.5; // represents upper bound of first wind speed bin (to be consistent with TAB file format)
            WS_IntSize = 1;

            filteringEnabled = true;

        }

        public int ThisCount
        {
            get {
                if (metItem == null)
                    return 0;
                else
                    return metItem.Length; }
        }     

        public void GetBinSettings(Continuum thisInst)
        {
            numWD = thisInst.GetNumWD();
            numTOD = Convert.ToInt16(thisInst.cboMCP_TOD.SelectedItem);
            numSeason = Convert.ToInt16(thisInst.cboMCP_Season.SelectedItem);
        }

        public Met.TOD GetTOD(DateTime thisDate)
        {
            Met.TOD thisTOD = Met.TOD.All;

            if (numTOD > 1)
            {
                if (thisDate.Hour >= dayStartHour && thisDate.Hour <= dayEndHour)
                    thisTOD = Met.TOD.Day;
                else
                    thisTOD = Met.TOD.Night;
            }
            

            return thisTOD;
        }

        public Met.Season GetSeason(DateTime thisDate)
        {
            Met.Season thisSeason = Met.Season.All;

            if (numSeason > 1)
            {
                if ((thisDate.Month == 12 || thisDate.Month == 1 || thisDate.Month == 2))
                    thisSeason = Met.Season.Winter;
                else if ((thisDate.Month == 3 || thisDate.Month == 4 || thisDate.Month == 5))
                    thisSeason = Met.Season.Spring;
                else if ((thisDate.Month == 6 || thisDate.Month == 7 || thisDate.Month == 8))
                    thisSeason = Met.Season.Summer;
                else if ((thisDate.Month == 9 || thisDate.Month == 10 || thisDate.Month == 11))
                    thisSeason = Met.Season.Fall;
            }
            
            return thisSeason;
        }
        
        public void MakeAllSameLength()
        {
            // if Met TAB files have different number of WS bins, this void adds zeros to the end to make all mets WS dists the same length
            int maxLength = 0;
                        
            for (int i = 0; i < ThisCount; i++)
            {
                for (int j = 0; j < metItem[i].WSWD_DistCount; j++)
                {
                    if (metItem[i].WSWD_Dists[j].WS_Dist.Length > maxLength)
                        maxLength = metItem[i].WSWD_Dists[j].WS_Dist.Length;
                }                              
            }

            for (int i = 0; i < ThisCount; i++)
            {
                for (int j = 0; j < metItem[i].WSWD_DistCount; j++)
                {
                    int thisLength = metItem[i].WSWD_Dists[j].WS_Dist.Length;
                    if (thisLength < maxLength)
                    {
                        Array.Resize(ref metItem[i].WSWD_Dists[j].WS_Dist, maxLength);

                        double[,] Orig_Sect_Dist = metItem[i].WSWD_Dists[j].sectorWS_Dist;
                        metItem[i].WSWD_Dists[j].sectorWS_Dist = new double[numWD, maxLength];

                        for (int WS_ind = 0; WS_ind <= Orig_Sect_Dist.GetUpperBound(0); WS_ind++)
                            for (int WD_Ind = 0; WD_Ind < numWD; WD_Ind++)
                                metItem[i].WSWD_Dists[j].sectorWS_Dist[WD_Ind, WS_ind] = Orig_Sect_Dist[WD_Ind, WS_ind];

                    }
                }
            }

        }

        public string MakeUpNameIfBlank()
        {
            // Creates a name for met site if blank in TAB file
            string metName = "Met_" + ThisCount;

            return metName;

        }

        public void AddMetTAB(string metName, double UTMX, double UTMY, int height, double[] metWindRose, double[,] sectorWS_Dist, double thisWS_FirstInt, double thisWS_IntSize, Continuum thisInst)
        {
            // Adds met to list, adds WSWD_Dist (All hours and All seasons for TAB file imports) and calculates average wind speed and directional WS ratios
            
            int newCount = ThisCount;
            Array.Resize(ref metItem, newCount + 1);

            WS_FirstInt = thisWS_FirstInt;
            WS_IntSize = thisWS_IntSize;

            metItem[newCount] = new Met();
            metItem[newCount].name = metName;
            metItem[newCount].UTMX = UTMX;
            metItem[newCount].UTMY = UTMY;

            numWD = metWindRose.Length;
            numWS = sectorWS_Dist.GetUpperBound(1) + 1;

            metItem[newCount].AddWSWD_DistFromTAB(Met.TOD.All, Met.Season.All, height, sectorWS_Dist, metWindRose);
            
            metItem[newCount].CalcAvgWS(thisInst);
            metItem[newCount].CalcSectorWS_Ratios(thisInst);
                       

        }

        public void FilterExtrapolateAddMetTimeSeries(string metName, double UTMX, double UTMY, Met_Data_Filter metData, Continuum thisInst)
        {
            int newCount = ThisCount;
            Array.Resize(ref metItem, newCount + 1);

            metItem[newCount] = new Met();
            metItem[newCount].name = metName;
            metItem[newCount].UTMX = UTMX;
            metItem[newCount].UTMY = UTMY;
            metItem[newCount].metData = metData;
            if (filteringEnabled)
                metItem[newCount].metData.FilterData("All");
            metItem[newCount].metData.EstimateAlpha();
            metItem[newCount].metData.ExtrapolateData(thisInst.modeledHeight);
                        
        }

        public void ExtrapolateAddMetTimeSeries(string metName, double UTMX, double UTMY, Met_Data_Filter metData, Continuum thisInst)
        {
            int newCount = ThisCount;
            Array.Resize(ref metItem, newCount + 1);

            metItem[newCount] = new Met();
            metItem[newCount].name = metName;
            metItem[newCount].UTMX = UTMX;
            metItem[newCount].UTMY = UTMY;
            metItem[newCount].metData = metData;            
            metItem[newCount].metData.ExtrapolateData(thisInst.modeledHeight);

        }

        public void Delete(string metname, Continuum thisInst, double metLat, double metLong)
        {
            // Deletes Met from list and data from DB (if time series)
            int newCount = ThisCount - 1;

            if (newCount > 0)
            {
                Met[] tempList = new Met[newCount]; // Create list of met towers that you//re keeping(so size one less than before)
                int tempIndex = 0;
                
                for (int i = 0; i < ThisCount; i++)
                {
                    if (metItem[i].name != metname)
                    {
                        tempList[tempIndex] = metItem[i];
                        tempIndex++;
                    }
                }
                metItem = tempList;
            }
            else {
                metItem = null;

            }

            // Delete data from DB
            NodeCollection nodeList = new NodeCollection();
            string connString = nodeList.GetDB_ConnectionString(thisInst.savedParams.savedFileName);
            try
            {
                using (var context = new Continuum_EDMContainer(connString))
                {
                    var anem_db = from N in context.Anem_table where N.metName == metname select N;

                    foreach (var N in anem_db)
                        context.Anem_table.Remove(N);

                    var vane_db = from N in context.Vane_table where N.metName == metname select N;

                    foreach (var N in vane_db)
                        context.Vane_table.Remove(N);

                    var temp_db = from N in context.Temp_table where N.metName == metname select N;

                    foreach (var N in temp_db)
                        context.Temp_table.Remove(N);

                    context.SaveChanges();                    
                }                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException.ToString());
                return;
            }

            // Delete unused MERRA data from DB
            // First check to see if there is MERRA data for this met
            MERRA thisMERRA = thisInst.merraList.GetMERRA(metLat, metLong);
            thisMERRA.GetMERRADataFromDB(thisInst);

            if (thisMERRA.MERRA_Nodes != null)
            {              
                // Go through each MERRA node and check to see if it is used by another met
                for (int i = 0; i < thisMERRA.numMERRA_Nodes; i++)
                {
                    bool usedByOtherMets = false;

                    for (int m = 0; m < ThisCount; m++)
                    {
                        UTM_conversion.Lat_Long theseLL = thisInst.UTM_conversions.UTMtoLL(metItem[m].UTMX, metItem[m].UTMY);
                        MERRA otherMERRA = thisInst.merraList.GetMERRA(theseLL.latitude, theseLL.longitude);

                        for (int j = 0; j < otherMERRA.numMERRA_Nodes; j++)
                            if (thisMERRA.MERRA_Nodes[i].XY_ind.Lat == otherMERRA.MERRA_Nodes[j].XY_ind.Lat
                                && thisMERRA.MERRA_Nodes[i].XY_ind.Lon == otherMERRA.MERRA_Nodes[j].XY_ind.Lon)
                                usedByOtherMets = true;
                    }

                    if (usedByOtherMets == false)
                        thisInst.merraList.DeleteMERRANodeDataFromDB(thisMERRA.MERRA_Nodes[i].XY_ind.Lat, thisMERRA.MERRA_Nodes[i].XY_ind.Lon, thisInst);

                }
            }

            // Delete MERRA data from MERRAList
            thisInst.merraList.deleteMERRA(metLat, metLong, thisInst);

        }

        public void DeleteAllTimeSeriesEsts(Continuum thisInst)
        {
            // Deletes all met site time series estimates. Called after a change is made to MCP or MERRA2 settings
            
            for (int i = 0; i < ThisCount; i++)
            {
                if (metItem[i].mcp != null)
                {
                    metItem[i].mcp.New_MCP(true, false, thisInst);
                    metItem[i].WSWD_Dists = new Met.WSWD_Dist[0];
                }

                metItem[i].metData.ClearAlphaAndSimulatedEstimates();
                metItem[i].WSWD_Dists = null;
            }               
                           
        }

        public void CalcMetExposures(int metInd, int radiusIndex, Continuum thisInst)
        {
            // Calculates exposure and SRDH at met site (if not already calculated)            
            int numSectors = 1;

            // First find elevation
            
            if (metItem[metInd].elev == 0) metItem[metInd].elev = thisInst.topo.CalcElevs(metItem[metInd].UTMX, metItem[metInd].UTMY);
            
            int thisRadius = thisInst.radiiList.investItem[radiusIndex].radius;
            double thisExponent = thisInst.radiiList.investItem[radiusIndex].exponent;
            double thisX = metItem[metInd].UTMX;
            double thisY = metItem[metInd].UTMY;

            bool isNew = metItem[metInd].IsNewExposure(thisRadius, thisExponent, numSectors);
            bool IsNewSRDH = metItem[metInd].IsNewSRDH(thisRadius, thisExponent, numSectors);

            if (isNew == true)
            {
                metItem[metInd].AddExposure(thisRadius, thisExponent, numSectors);

                // Check to see if an exposure with a smaller radii has been calculated
                int smallerRadius = thisInst.topo.GetSmallerRadius(metItem[metInd].expo, thisRadius, thisExponent, numSectors);

                if (smallerRadius == 0 || numSectors > 1)
                { // when sector avg is used, can't add on to exposure calcs...so gotta do it the long way
                    metItem[metInd].expo[radiusIndex] = thisInst.topo.CalcExposures(thisX, thisY, metItem[metInd].elev, thisRadius, thisExponent, numSectors, thisInst.topo, numWD);
                    if (thisInst.topo.gotSR == true)
                        thisInst.topo.CalcSRDH(ref metItem[metInd].expo[radiusIndex], thisX, thisY, thisRadius, thisExponent, numWD);
                }
                else
                {
                    Exposure smallerExposure = thisInst.topo.GetSmallerRadiusExpo(metItem[metInd].expo, smallerRadius, metItem[metInd].expo[radiusIndex].exponent, numSectors);

                    metItem[metInd].expo[radiusIndex] = thisInst.topo.CalcExposuresWithSmallerRadius(thisX, thisY, metItem[metInd].elev, thisRadius, thisExponent, numSectors, smallerRadius, smallerExposure, numWD);

                    if (thisInst.topo.gotSR == true)
                        thisInst.topo.CalcSRDHwithSmallerRadius(ref metItem[metInd].expo[radiusIndex], thisX, thisY, thisRadius, thisExponent, numSectors, smallerRadius, smallerExposure, numWD);
                    
                }
            }

            if (IsNewSRDH == true)
            {
                if (thisInst.topo.gotSR == true)
                {   
                    // Check to see if an exposure with a smaller radii has been calculated
                    int smallerRadius = thisInst.topo.GetSmallerRadius(metItem[metInd].expo, thisRadius, thisExponent, numSectors);

                    if (smallerRadius == 0)
                        thisInst.topo.CalcSRDH(ref metItem[metInd].expo[radiusIndex], thisX, thisY, thisRadius, thisExponent, numWD);
                    else {
                        Exposure smallerExposure = thisInst.topo.GetSmallerRadiusExpo(metItem[metInd].expo, smallerRadius, metItem[metInd].expo[radiusIndex].exponent, numSectors);
                        thisInst.topo.CalcSRDHwithSmallerRadius(ref metItem[metInd].expo[radiusIndex], thisX, thisY, thisRadius, thisExponent, numSectors, smallerRadius, smallerExposure, numWD);
                    }

                }                
            }

            // Upwind parallel and crosswind P10 grades by direction sector. Not currently used. Intended for use in 'flow around hills' model
            if (metItem[metInd].expo[radiusIndex].UW_P10CrossGrade == null)
            {
                // Calc P10 UW Crosswind Grade
                
                metItem[metInd].expo[radiusIndex].UW_P10CrossGrade = new double[numWD];
                metItem[metInd].expo[radiusIndex].UW_ParallelGrade = new double[numWD];

                for (int r = 0; r < numWD; r++)
                {
                    double UW_CW_Grade = thisInst.topo.CalcP10_UW_CrosswindGrade(metItem[metInd].UTMX, metItem[metInd].UTMY, thisInst.radiiList, r, numWD);
                    double UW_PL_Grade = thisInst.topo.CalcP10_UW_ParallelGrade(metItem[metInd].UTMX, metItem[metInd].UTMY, thisInst.radiiList, r, numWD);

                    metItem[metInd].expo[radiusIndex].UW_P10CrossGrade[r] = UW_CW_Grade;
                    metItem[metInd].expo[radiusIndex].UW_ParallelGrade[r] = UW_PL_Grade;
                }
            }
        }

        public void ReCalcSRDH(TopoInfo topo, InvestCollection radiiList)
        {
            if (expoIsCalc == false)
                return;

            // if Land Cover key is changed, the surface roughness and displacement heights are recalculated in this void
            int expoInd = 0;
            int numSectors = 1;

            for (int metInd = 0; metInd < ThisCount; metInd++)
            {
                // First find elevation
                if (metItem[metInd].elev == 0) metItem[metInd].elev = topo.CalcElevs(metItem[metInd].UTMX, metItem[metInd].UTMY);                              
                         
                double thisX = metItem[metInd].UTMX;
                double thisY = metItem[metInd].UTMY;

                for (int radiusInd = 0; radiusInd < radiiList.ThisCount; radiusInd++)
                {
                    int thisRadius = radiiList.investItem[radiusInd].radius;
                    double thisExponent = radiiList.investItem[radiusInd].exponent;

                    if (topo.gotSR == true)
                    {
                        for (int k = 0; k < metItem[metInd].ExposureCount; k++)
                        {
                            if (metItem[metInd].expo[k].radius == thisRadius && metItem[metInd].expo[k].exponent == thisExponent)
                            {
                                expoInd = k;
                                break;
                            }
                        }

                        // Check to see if an exposure with a smaller radii has been calculated
                        int smallerRadius = topo.GetSmallerRadius(metItem[metInd].expo, thisRadius, thisExponent, numSectors);

                        if (smallerRadius == 0)
                            topo.CalcSRDH(ref metItem[metInd].expo[expoInd], thisX, thisY, thisRadius, thisExponent, numWD);
                        else {
                            Exposure smallerExposure = topo.GetSmallerRadiusExpo(metItem[metInd].expo, smallerRadius, metItem[metInd].expo[expoInd].exponent, numSectors);
                            topo.CalcSRDHwithSmallerRadius(ref metItem[metInd].expo[expoInd], thisX, thisY, thisRadius, thisExponent, numSectors, smallerRadius, smallerExposure, numWD);
                        }
                    }
                }
            }
            SRDH_IsCalc = true;
        }
                

        public double[] GetAvgWindRoseMetsUsed(string[] metsUsed, Met.TOD thisTOD, Met.Season thisSeason, double thisHeight)
        {
            // Returns average wind rose calculated from metsUsed
            double[] avgWindRose = null;
            
            bool metIsUsed = false;

            if (ThisCount > 0)
            {
                if (metItem[0].WSWD_DistCount == 0) return avgWindRose;
                
                avgWindRose = new double[numWD];

                for (int i = 0; i < ThisCount; i++)
                {
                    metIsUsed = false;
                    Met.WSWD_Dist thisDist = new Met.WSWD_Dist();

                    for (int metInd = 0; metInd < metsUsed.Length; metInd++)
                    {
                        if (metItem[i].name == metsUsed[metInd])
                        {
                            thisDist = metItem[i].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                            metIsUsed = true;
                            break;
                        }
                    }

                    if (metIsUsed == true)
                        for (int j = 0; j < numWD; j++)
                            avgWindRose[j] = avgWindRose[j] + thisDist.windRose[j];
                                        
                }

                for (int i = 0; i < numWD; i++)
                    avgWindRose[i] = avgWindRose[i] / metsUsed.Length;
            }

            return avgWindRose;

        }

        public double[] GetAvgWindRose(double thisHeight, Met.TOD thisTOD, Met.Season thisSeason)
        {
            // Returns average wind rose using all mets in list
            double[] avgWindRose = null;
             

            if (ThisCount > 0)
            {                
                avgWindRose = new double[numWD];

                for (int i = 0; i < ThisCount; i++)
                {
                    Met.WSWD_Dist thisDist = metItem[i].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                    for (int j = 0; j < numWD; j++)
                        avgWindRose[j] = avgWindRose[j] + thisDist.windRose[j];
                }

                for (int i = 0; i < numWD; i++)
                    avgWindRose[i] = avgWindRose[i] / ThisCount;
            }

            return avgWindRose;

        }

        public double[] GetInterpolatedWindRose(string[] metsUsed, double UTMX, double UTMY, Met.TOD thisTOD, Met.Season thisSeason, double thisHeight)
        {
            // Returns wind rose interpolated from mets in metsUsed and weighted based on distance to X/Y
            double[] thisWR = null; 
            TopoInfo topo = new TopoInfo();

            if (ThisCount > 0)
            {                
                thisWR = new double[numWD];
                double[] wgts = new double[numWD];
                
                for (int i = 0; i < ThisCount; i++)
                { 
                    for (int j = 0; j < metsUsed.Length; j++)
                    {
                        if (metItem[i].name == metsUsed[j])
                        {
                            double thisDistance = topo.CalcDistanceBetweenPoints(UTMX, UTMY, metItem[i].UTMX, metItem[i].UTMY);

                            if (thisDistance == 0) thisDistance = 1;

                            for (int WD_Ind = 0; WD_Ind <= numWD - 1; WD_Ind++)
                            {
                                Met.WSWD_Dist thisDist = metItem[i].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                                thisWR[WD_Ind] = thisWR[WD_Ind] + thisDist.windRose[WD_Ind] * 1 / thisDistance;
                                wgts[WD_Ind] = wgts[WD_Ind] + 1 / thisDistance;
                            }
                            break;
                        }
                    }
                }

                for (int WD_Ind = 0; WD_Ind <= numWD - 1; WD_Ind++)
                    thisWR[WD_Ind] = thisWR[WD_Ind] / wgts[WD_Ind];
            }

            return thisWR;
        }


        public double[] CalcOverallWS_Dist(double[,] sectorWS_Dist, double[] windRose)
        {
            // Calculate overall WS distribution using sectorwise wind speed distribution and weighted by wind rose                       
            double[] WS_Dist = null;

            if (sectorWS_Dist == null) return WS_Dist;  
            
            WS_Dist = new double[numWS];
            
            for (int i = 0; i < numWS; i++)
            {
                double sumRose = 0;
                double thisFreq = 0;
                for (int j = 0; j < numWD; j++)
                {
                    thisFreq = thisFreq + sectorWS_Dist[j, i] * windRose[j];
                    sumRose = sumRose + windRose[j];
                }
                WS_Dist[i] = thisFreq / sumRose;
            }

            return WS_Dist;
        }


        public double[] CalcWS_DistForTurbOrMap(string[] metsUsed, double avgWS, int WD_Ind, Met.TOD thisTOD, Met.Season thisSeason, double thisHeight)
        {
            // Calculates and returns a WS distribution by combining wind speed distributions from the metsUsed with calculated weights. Weights are adjusted until avgWS is matched
            // if avgWS is within range of met avg wind speeds, use weighted average of all mets where weights are altered until avgWS is reached
            // if avgWS is outside range of met WS then it uses the met with either the highest or lowest WS and adjusts WS dist until avgWS is reached
            
            if (metsUsed == null || avgWS == 0) return null;
            double[] WS_Dist = new double[numWS];

            int numMetsUsed = metsUsed.Length;
            Met[] metsForDist = GetMets(metsUsed, null);
                        
            double calcWS = 0;  // Average wind speed of estimated wind speed distribution          
            double avgWeightSum = 0; // Sum of WS distribution (used to find mean WS) 
            
            Met[] minMaxWS_Mets = GetMetsWithMinMaxWS(metsUsed, WD_Ind, thisTOD, thisSeason, thisHeight); // 0: Met with minimum WS; 1: Met with maximum WS
            Met.WSWD_Dist[] minMaxWS_MetsDists = new Met.WSWD_Dist[2];
            minMaxWS_MetsDists[0] = minMaxWS_Mets[0].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
            minMaxWS_MetsDists[1] = minMaxWS_Mets[1].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);

            double[] minMaxWS = new double[2]; // Average wind speeds of mets with min/max WS
            
            if (WD_Ind == numWD)
            {
                minMaxWS[0] = minMaxWS_MetsDists[0].WS;
                minMaxWS[1] = minMaxWS_MetsDists[1].WS;
            }
            else
            {
                minMaxWS[0] = minMaxWS_MetsDists[0].WS * minMaxWS_MetsDists[0].sectorWS_Ratio[WD_Ind];
                minMaxWS[1] = minMaxWS_MetsDists[1].WS * minMaxWS_MetsDists[1].sectorWS_Ratio[WD_Ind];
            }

            bool avgWS_InRange = false;
            if (avgWS > minMaxWS[0] && avgWS < minMaxWS[1])
                avgWS_InRange = true;                     

            if (avgWS_InRange == true)
            { // use combination of all mets used to form WS distribution

                double WS_diff = calcWS - avgWS;
                double[] weightSum = new double[numWS];
                double[] metWeights = new double[numMetsUsed];

                // First, combine all met WS distributions using met weights = 1
                for (int i = 0; i < numMetsUsed; i++)
                {
                    Met.WSWD_Dist thisDist = metsForDist[i].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                    metWeights[i] = 1;
                    for (int j = 0; j < numWS; j++)
                    {
                        if (WD_Ind == numWD) {
                            WS_Dist[j] = WS_Dist[j] + thisDist.WS_Dist[j];
                            weightSum[j] = weightSum[j] + metWeights[i];
                        }
                        else {
                            WS_Dist[j] = WS_Dist[j] + thisDist.sectorWS_Dist[WD_Ind, j];
                            weightSum[j] = weightSum[j] + metWeights[i];
                        }
                    }
                }

                calcWS = 0;
                avgWeightSum = 0;
                for (int i = 0; i < numWS; i++)
                {
                    WS_Dist[i] = WS_Dist[i] / weightSum[i];
                    calcWS = calcWS + WS_Dist[i] * GetWS_atWS_Ind(i);
                    avgWeightSum = avgWeightSum + WS_Dist[i];
                }

                // Make sure it adds up to 1.0                
                for (int i = 0; i < numWS; i++)
                    WS_Dist[i] = WS_Dist[i] / avgWeightSum;

                calcWS = calcWS / avgWeightSum;
                WS_diff = calcWS - avgWS;

                double lastWeight;
                int counter = 0;

                while (Math.Abs(WS_diff) > 0.001 && counter < 200)
                {
                    counter++;

                    for (int j = 0; j < numWS; j++)
                    {
                        WS_Dist[j] = 0;
                        weightSum[j] = 0;
                    }

                    for (int i = 0; i < numMetsUsed; i++)
                    {
                        Met.WSWD_Dist thisDist = metsForDist[i].GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                        lastWeight = metWeights[i];
                        if (metWeights[i] == 0) metWeights[i] = 0.001f;

                        double metWS = 0;
                        if (WD_Ind == numWD)
                            metWS = thisDist.WS;
                        else
                            metWS = thisDist.WS * thisDist.sectorWS_Ratio[WD_Ind];

                        if ((WS_diff < 0 && metWS < avgWS) || (WS_diff > 0 && metWS > avgWS))
                        { // reduce met weight
                            metWeights[i] = metWeights[i] - metWeights[i] * Math.Abs(WS_diff) * (1 - Math.Abs((avgWS - metWS) / avgWS));
                            if (metWeights[i] > lastWeight) metWeights[i] = 0;
                        }
                        else if ((WS_diff > 0 && metWS < avgWS) || (WS_diff < 0 && metWS > avgWS))
                        { // increase met weight                            
                            metWeights[i] = metWeights[i] + metWeights[i] * Math.Abs(WS_diff) * (1 - Math.Abs((avgWS - metWS) / avgWS));
                            if (metWeights[i] < lastWeight) metWeights[i] = 0;
                        }                        

                        if (metWeights[i] < 0) metWeights[i] = 0.001f;

                        for (int j = 0; j < numWS; j++)
                        {
                            if (WD_Ind == numWD)
                                WS_Dist[j] = WS_Dist[j] + thisDist.WS_Dist[j] * metWeights[i];
                            else
                                WS_Dist[j] = WS_Dist[j] + thisDist.sectorWS_Dist[WD_Ind, j] * metWeights[i];

                            weightSum[j] = weightSum[j] + metWeights[i];
                        }
                    }

                    calcWS = 0;
                    avgWeightSum = 0;

                    for (int i = 0; i < numWS; i++)
                    {
                        WS_Dist[i] = WS_Dist[i] / weightSum[i];
                        calcWS = calcWS + WS_Dist[i] * GetWS_atWS_Ind(i);
                        avgWeightSum = avgWeightSum + WS_Dist[i];
                    }

                    if (avgWeightSum < 0.999 || avgWeightSum > 1.001)
                        for (int i = 0; i < numWS; i++)
                            WS_Dist[i] = WS_Dist[i] * Convert.ToSingle(1.0 / avgWeightSum);

                    calcWS = calcWS / avgWeightSum;
                    WS_diff = calcWS - avgWS;
                }
            }

            else { // use either min or max met and adjust WS dist to form WS dist with avgWS
                calcWS = 0;
                avgWeightSum = 0;
                                
                if (avgWS < minMaxWS[0])
                {
                    for (int i = 0; i < numWS; i++)
                    {
                        if (WD_Ind == numWD)
                            WS_Dist = minMaxWS_MetsDists[0].WS_Dist;
                        else
                            WS_Dist[i] = minMaxWS_MetsDists[0].sectorWS_Dist[WD_Ind, i];

                        calcWS = calcWS + WS_Dist[i] * GetWS_atWS_Ind(i);
                        avgWeightSum = avgWeightSum + WS_Dist[i];
                    }
                }
                else
                {                        
                    for (int i = 0; i < numWS; i++)
                    {
                        if (WD_Ind == numWD)
                            WS_Dist = minMaxWS_MetsDists[1].WS_Dist;
                        else
                            WS_Dist[i] = minMaxWS_MetsDists[1].sectorWS_Dist[WD_Ind, i];
                        calcWS = calcWS + WS_Dist[i] * GetWS_atWS_Ind(i);
                        avgWeightSum = avgWeightSum + WS_Dist[i];
                    }
                }                          

                // Make sure it adds up to 1.0
                if (avgWeightSum < 0.999 || avgWeightSum > 1.001)
                    for (int i = 0; i <= numWS - 1; i++)
                        WS_Dist[i] = WS_Dist[i] * 1.0f / avgWeightSum;


                calcWS = calcWS / avgWeightSum;
                double WS_diff = calcWS - avgWS;

                int avgWS_rnd = Convert.ToInt16(Math.Round(avgWS, 0));
                int counter = 0;

                while (Math.Abs(WS_diff) > 0.001 && counter < 200)
                {
                    counter++;
                    avgWeightSum = 0;                                      

                    for (int i = 0; i < numWS; i++)
                    {
                        double thisWS = GetWS_atWS_Ind(i);
                        if ((thisWS < avgWS_rnd && WS_diff < 0) || (thisWS > avgWS_rnd && WS_diff > 0))
                        { // reduce freq 
                            WS_Dist[i] = WS_Dist[i] - WS_Dist[i] * Math.Abs(WS_diff) * 0.06f;
                            avgWeightSum = avgWeightSum + WS_Dist[i];
                        }
                        else if ((thisWS < avgWS_rnd && WS_diff > 0) || (thisWS > avgWS_rnd && WS_diff < 0))
                        { // increase freq
                            WS_Dist[i] = WS_Dist[i] + WS_Dist[i] * Math.Abs(WS_diff) * 0.06f;
                            avgWeightSum = avgWeightSum + WS_Dist[i];
                        }
                        else if (thisWS == avgWS_rnd)
                        {
                            WS_Dist[i] = WS_Dist[i];
                            avgWeightSum = avgWeightSum + WS_Dist[i];
                        }
                    }

                    // Make sure it adds up to 1.0
                    if (avgWeightSum < 0.999 || avgWeightSum > 1.001)
                        for (int i = 0; i < numWS; i++)
                            WS_Dist[i] = WS_Dist[i] * 1.0f / avgWeightSum;

                    calcWS = 0;
                    avgWeightSum = 0;
                    for (int i = 0; i < numWS; i++)
                    {
                        calcWS = calcWS + WS_Dist[i] * GetWS_atWS_Ind(i);
                        avgWeightSum = avgWeightSum + WS_Dist[i];
                    }

                    calcWS = calcWS / avgWeightSum;
                    WS_diff = calcWS - avgWS;
                }
            }

            return WS_Dist;
        }               

        public Met[] GetMetsWithMinMaxWS(string[] metsUsed, int WD_Ind, Met.TOD thisTOD, Met.Season thisSeason, double thisHeight)
        {
            Met[] minMaxWS = new Met[2];
            Met.WSWD_Dist[] minMaxWSDist = new Met.WSWD_Dist[2];
                        
            if (metsUsed == null) return minMaxWS;
            int numMetsUsed = metsUsed.Length;
            
            for (int i = 0; i < ThisCount; i++)
            {
                Met thisMet = metItem[i];
                
                for (int j = 0; j < numMetsUsed; j++)
                {
                    if (thisMet.name == metsUsed[j])
                    {
                        Met.WSWD_Dist thisDist = thisMet.GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                        if (WD_Ind == numWD)
                        {
                            if (minMaxWS[0] == null)
                            {
                                minMaxWS[0] = thisMet;
                                minMaxWSDist[0] = thisMet.GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                            }                                
                            else if (thisDist.WS < minMaxWSDist[0].WS)
                            {
                                minMaxWS[0] = thisMet;
                                minMaxWSDist[0] = thisDist;
                            }                                

                            if (minMaxWS[1] == null)
                            {
                                minMaxWS[1] = thisMet;
                                minMaxWSDist[1] = thisMet.GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                            }                                
                            else if (thisDist.WS > minMaxWSDist[1].WS)
                            {
                                minMaxWS[1] = thisMet;
                                minMaxWSDist[1] = thisDist;
                            }
                                
                        }
                        else
                        {
                            if (minMaxWS[0] == null)
                            {
                                minMaxWS[0] = thisMet;
                                minMaxWSDist[0] = thisMet.GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                            }
                                
                            else if (thisDist.WS * thisDist.sectorWS_Ratio[WD_Ind] < minMaxWSDist[0].WS * minMaxWSDist[0].sectorWS_Ratio[WD_Ind])
                            {
                                minMaxWS[0] = thisMet;
                                minMaxWSDist[0] = thisDist;
                            }                                

                            if (minMaxWS[1] == null)
                            {
                                minMaxWS[1] = thisMet;
                                minMaxWSDist[1] = thisMet.GetWS_WD_Dist(thisHeight, thisTOD, thisSeason);
                            }                                
                            else if (thisDist.WS * thisDist.sectorWS_Ratio[WD_Ind] > minMaxWSDist[1].WS * minMaxWSDist[1].sectorWS_Ratio[WD_Ind])
                            {
                                minMaxWS[1] = thisMet;
                                minMaxWSDist[1] = thisDist;
                            }                                
                        }

                        break;
                    }
                }
            }

            return minMaxWS;
        }

        public double GetWS_atWS_Ind(int thisInd)
        {
            double thisWS = 0;
            if (ThisCount == 0) return thisWS;                      

            thisWS = WS_FirstInt + WS_IntSize * thisInd - WS_IntSize / 2;

            return thisWS;
        }

        public Met[] GetMets(string[] metNames, string[] exceptMets)
        {
            // Returns Met objects for names listed
            Met[] theseMets = null;
            int numToExcl;

            if (metNames == null) return theseMets;
            
            if (exceptMets == null) 
                numToExcl = 0;
            else
                numToExcl = exceptMets.Length;
            
            int numMetsToReturn = metNames.Length - numToExcl;
            theseMets = new Met[numMetsToReturn];
            int metInd = 0;
            bool includeMet = true;
            
            for (int i = 0; i < metNames.Length; i++)
            { 
                for (int j = 0; j < metItem.Length; j++)
                {
                    if (metNames[i] == metItem[j].name)
                    {
                        includeMet = true;
                        for (int k = 0; k < numToExcl; k++)
                        {
                            if (metNames[i] == exceptMets[k])
                            {
                                includeMet = false;
                                break;
                            }
                        }

                        if (includeMet == true)
                        {
                            theseMets[metInd] = metItem[j];
                            metInd++;
                        }

                        break;
                    }

                }
            }

            return theseMets;
        }

        public Met GetMet(string metName)
        {
            // Returns Met objects for names listed
            Met thisMet = new Met();
            
            if (metName == "") return thisMet;
                        
            for (int i = 0; i < metItem.Length; i++)
            {
                if (metName == metItem[i].name)
                {
                    thisMet = metItem[i];
                    break;
                }
            }
            
            return thisMet;
        }

        public string[] GetMetsUsed()
            {
                // Returns array of met names from list of mets used in model
                string[] metsUsed = new string[ThisCount];

                for (int i = 0; i < ThisCount; i++)
                    metsUsed[i] = metItem[i].name;
            
                return metsUsed;
        }

        public Weibull_params CalcWeibullParams(double[] WS_Dist, double[,] sectDist, double avgWS)
        {
            // Calculates the RMS difference in weibull Freq dist and WS dist then sweeps k value until the min is found. Returns weibull estimates for overall and sector
            Weibull_params weibull = new Weibull_params();

            if (ThisCount == 0 || WS_Dist == null || sectDist == null)
                return weibull;       
                                               
            double K_Min_RMS = 0;
            double freqDiffMin = 0;
            
            // Sweep k from 1.5 to 3.5 and find k with min freq diff
            for (double k = 1.5f; k <= 3.5f; k = k + 0.5f)
            {
                double freqDiffSqr = 0;
                weibull.overall_k = k;
                double m = 1 + 1 / weibull.overall_k;                
                weibull.overall_A = CalcWeibullA(avgWS, m);
                double[] dist = new double[numWS];

                for (int j = 0; j < numWS; j++)
                {
                    double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;
                    dist[j] = CalcWeibullDist(weibull.overall_k, weibull.overall_A, thisWS);
                    freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((WS_Dist[j] - dist[j]), 2));
                }

                freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));
                if (k == 1.5)
                {
                    freqDiffMin = freqDiffSqr;
                    K_Min_RMS = 1.5f;
                }
                else if (freqDiffSqr < freqDiffMin)
                {
                    freqDiffMin = freqDiffSqr;
                    K_Min_RMS = k;
                }
            }

            // Sweep k from Last Min k - 0.5 to Last Min k + 0.5 and find k with min freq diff
            for (double k = K_Min_RMS - 0.5f; k <= K_Min_RMS + 0.5f; k = k + 0.1f)
            {
                double freqDiffSqr = 0;
                weibull.overall_k = k;
                double m = 1 + 1 / weibull.overall_k;
                weibull.overall_A = CalcWeibullA(avgWS, m);

                double[] dist = new double[numWS];
                for (int j = 0; j < numWS; j++)
                {
                    double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;
                    dist[j] = CalcWeibullDist(weibull.overall_k, weibull.overall_A, thisWS);
                    freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((WS_Dist[j] - dist[j]), 2));
                }

                freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));
                if ( freqDiffSqr<freqDiffMin )
                {
                    freqDiffMin = freqDiffSqr;
                    K_Min_RMS = k;
                }
            }

            // Sweep k from Last Min k - 0.1 to Last Min k + 0.1 and find k with min freq diff
            for (double k = K_Min_RMS - 0.1f; k <= K_Min_RMS + 0.1f; k = k + 0.02f)
            {
                double freqDiffSqr = 0;
                weibull.overall_k = k;
                double m = 1 + 1 / weibull.overall_k;
                weibull.overall_A = CalcWeibullA(avgWS, m);

                double[] dist = new double[numWS];
                for (int j = 0; j < numWS; j++)
                {
                    double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;
                    dist[j] = CalcWeibullDist(weibull.overall_k, weibull.overall_A, thisWS); ;
                    freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((WS_Dist[j] - dist[j]), 2));
                }

                freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));
                if (freqDiffSqr<freqDiffMin)
                {
                    freqDiffMin = freqDiffSqr;
                    K_Min_RMS = k;
                }
            }

            weibull.overall_k = K_Min_RMS;            
            weibull.overall_A = CalcWeibullA(avgWS, 1 + 1 / weibull.overall_k);

            // Now do sectorwise
            weibull.sector_k = new double[numWD];
            weibull.sector_A = new double[numWD];
            
            for (int i = 0; i < numWD; i++)
            {
                // Need to calc avg WS in this sector
                double avgSectWS = 0;
                double sectSum = 0;              
                freqDiffMin = 0;
                double freqDiffSqr = 0;
                for (int j = 0; j < numWS; j++)
                {
                    avgSectWS = avgSectWS + sectDist[i, j] * (WS_FirstInt + WS_IntSize * j - WS_IntSize / 2);
                    sectSum = sectSum + sectDist[i, j];
                }
                avgSectWS = avgSectWS / sectSum;

                // Sweep k from 1.5 to 3.5 and find k with min freq diff
                for (double k = 1.5f; k <= 3.5f; k = k + 0.5f)
                {
                    freqDiffSqr = 0;
                    weibull.sector_k[i] = k;
                    double m = 1 + 1 / weibull.sector_k[i];
                    weibull.sector_A[i] = CalcWeibullA(avgSectWS, m);

                    double[] dist = new double[numWS];
                    for (int j = 0; j < numWS; j++)
                    {
                        double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;                        
                        dist[j] = CalcWeibullDist(weibull.sector_k[i], weibull.sector_A[i], thisWS);
                        freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((sectDist[i, j] - dist[j]), 2));
                    }

                    freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));

                    if (k == 1.5)
                    {
                        freqDiffMin = freqDiffSqr;
                        K_Min_RMS = 1.5f;
                    }
                    else if (freqDiffSqr < freqDiffMin)
                    {
                        freqDiffMin = freqDiffSqr;
                        K_Min_RMS = k;
                    }
                }

                // Sweep k from Last Min k - 0.5 to Last Min k + 0.5 and find k with min freq diff
                for (double k = K_Min_RMS - 0.5f; k <= K_Min_RMS + 0.5f; k = k + 0.1f)
                {
                    freqDiffSqr = 0;
                    weibull.sector_k[i] = k;
                    double m = 1 + 1 / weibull.sector_k[i];
                    weibull.sector_A[i] = CalcWeibullA(avgSectWS, m);

                    double[] dist = new double[numWS];
                    for (int j = 0; j < numWS; j++)
                    {
                        double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;
                        dist[j] = CalcWeibullDist(weibull.sector_k[i], weibull.sector_A[i], thisWS);
                        freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((sectDist[i, j] - dist[j]), 2));
                    }

                    freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));

                    if (freqDiffSqr < freqDiffMin)
                    {
                        freqDiffMin = freqDiffSqr;
                        K_Min_RMS = k;
                    }
                }

                // Sweep k from Last Min k - 0.1 to Last Min k + 0.1 and find k with min freq diff
                for (double k = K_Min_RMS - 0.1f; k <= K_Min_RMS + 0.1f; k = k + 0.02f)
                {
                    freqDiffSqr = 0;
                    weibull.sector_k[i] = k;
                    double m = 1 + 1 / weibull.sector_k[i];
                    weibull.sector_A[i] = CalcWeibullA(avgSectWS, m);

                    double[] dist = new double[numWS];
                    for (int j = 0; j < numWS; j++)
                    {
                        double thisWS = WS_FirstInt + WS_IntSize * j - WS_IntSize / 2;
                        dist[j] = CalcWeibullDist(weibull.sector_k[i], weibull.sector_A[i], thisWS);
                        freqDiffSqr = freqDiffSqr + Convert.ToSingle(Math.Pow((sectDist[i, j] - dist[j]), 2));
                    }

                    freqDiffSqr = Convert.ToSingle(Math.Pow((freqDiffSqr / numWS), 0.5));
                    if (freqDiffSqr < freqDiffMin)
                    {
                        freqDiffMin = freqDiffSqr;
                        K_Min_RMS = k;
                    }
                }

                weibull.sector_k[i] = K_Min_RMS;
                double This_m = 1 + 1 / weibull.sector_k[i];
                weibull.sector_A[i] = CalcWeibullA(avgSectWS, This_m);
            }

            return weibull;
        }

        [Serializable()]
        public struct Weibull_params
        {
            public double overall_k;
            public double overall_A;
            public double[] sector_k;
            public double[] sector_A;
        }

        public double CalcWeibullA(double avgWS, double m)
        {
    
            double weibullA = avgWS / Convert.ToSingle((Math.Pow((2 * Math.PI * m), 0.5) * Math.Pow(m, (m - 1)) * Math.Exp(-m) * (1 + 1 / (12 * m) + 1 /
                            (288 * Math.Pow(m, 2)) - 139 / (51840 * Math.Pow(m, 3)))));

            return weibullA;
        }

        public double CalcWeibullDist(double k, double A, double WS)
        {
            double weibullDist = Convert.ToSingle((k / A) * Math.Pow((WS / A), (k - 1)) * Math.Exp(-(Math.Pow((WS / A), k))));
            return weibullDist;
        }

        public bool sameMets(string[] metsUsed1, string[] metsUsed2)
        {
            // Returns true if metsUsed1 and metsUsed2 have same mets            
            bool sameMetSites = false;
            bool foundMet = false;

            if (metsUsed1 != null && metsUsed2 != null)
            {
                if (metsUsed1.Length == metsUsed2.Length)
                {
                    for (int j = 0; j < metsUsed1.Length; j++)
                    {
                        foundMet = false;
                        for (int i = 0; i < metsUsed2.Length; i++)
                        {
                            if (metsUsed1[j] == metsUsed2[i])
                            {
                                foundMet = true;
                                break;
                            }
                        }

                        if (foundMet == false)
                        {
                            sameMetSites = false;
                            break;
                        }
                        else
                            sameMetSites = true;

                    }
                }
                else
                    sameMetSites = false; // different lengths
            }
            else 
                sameMetSites = false;

            return sameMetSites;
        }        

        public string CreateMetString(string[] metsUsed, bool isVerbose)
        {
            // Returns string of all mets in metsUsed
            int numMetsUsed;
            string metString = "";

            if (metsUsed != null)
            {
                numMetsUsed = metsUsed.Length;

                if (numMetsUsed == ThisCount && isVerbose == false)
                    metString = "All Mets";
                else
                {
                    metString = metsUsed[0];
                    if (numMetsUsed > 1)
                    {
                        for (int i = 1; i < numMetsUsed; i++)
                            metString = metString + " " + metsUsed[i];
                    }

                }
            }
            else
                metString = "All Mets";
            
            return metString;

        }

        public void ClearAllMets(Continuum thisInst, bool clearDB)
        {
            // Clears list of mets
            metItem = null;
            expoIsCalc = false;
            SRDH_IsCalc = false;

            // If time series data, clears DB
            if (isTimeSeries && thisInst.savedParams.savedFileName != "" && clearDB)
            {
                NodeCollection nodeList = new NodeCollection();
                string connString = nodeList.GetDB_ConnectionString(thisInst.savedParams.savedFileName);

                try
                {
                    using (var context = new Continuum_EDMContainer(connString))
                    {
                        context.Database.ExecuteSqlCommand("TRUNCATE TABLE Anem_table");
                        context.Database.ExecuteSqlCommand("TRUNCATE TABLE Vane_table");
                        context.Database.ExecuteSqlCommand("TRUNCATE TABLE Temp_table");
                        context.SaveChanges();                        
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.InnerException.ToString());
                    return;
                }
            }
            
        }

        public void ClearAllExposuresAndGridStats()
        { 
            // Clears all calculated exposures, SRDH, and grid stats
            if (ThisCount > 0)
            {
                for (int i = 0; i < ThisCount; i++)
                {
                    metItem[i].ClearExposures();
                    metItem[i].gridStats.stats = null;
                }
            }
        }
                


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Reads in met data formatted with following convention: Anem_HH_A_XXX: HH = height of sensor,
        /// A = ID (A or B), XXX = avg, SD, min or max Vane_HH_XXX: HH = height, XX = avg, SD, min, or
        /// max Temp_HH_XXX_F: HH = height, XXX = avg, SD, min or max, F = C (celsius) or F (Farenheit)
        /// </summary>
        ///
        /// <remarks>   Liz, 6/23/2017. </remarks>
        ///
        /// <param name="filename"> Filename of the met data file to import. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public bool ImportFilterExtrapMetDataContinuum(string filename, Continuum thisInst)
        {
            // Reads in formatted .csv file with anem, vane, and temp data. Adds the new met to metList. 
            // Filters (using all filters) and extrapolates to modeled height (specified on Input tab)
            // Returns true if met data successfully read in, filtered, extrapolated, and added
            
            Met_Data_Filter thisMetData = new Met_Data_Filter();
            String[] header;
            Header_Details[] headerDeets = new Header_Details[0];
            string line;
            Char[] delims = { ',' };
            Check_class check = new Check_class();

            if (filename != "")
            {
                StreamReader file;
                try
                {
                    file = new StreamReader(filename);
                }
                catch
                {
                    MessageBox.Show("Error opening the met data file. Check that it's not open in another program.", "", MessageBoxButtons.OK);                    
                    return false;
                }
                int firstInd = filename.LastIndexOf('\\') + 1;
                int fileLength = filename.Length - filename.LastIndexOf('\\') - 1;
                
                // first read in header and figure out how many sensors and heights and initiliaze anem/vane/temp objects
                try
                {
                    // first line contains name of met site
                    line = file.ReadLine();
                    string thisName = line.Trim(',');

                    bool inputMet = check.CheckMetName(thisName, thisInst.turbineList);

                    if (inputMet == false)
                        return false;

                    // second line contains WS units, latitude and longitude
                    line = file.ReadLine();
                    string[] firstLine = line.Split(delims);
                    thisMetData.WS_units = firstLine[1];

                    double thisLat = Convert.ToDouble(firstLine[3]);
                    double thisLong = Convert.ToDouble(firstLine[5]);

                    if (thisLat > 100)
                    {
                        MessageBox.Show("Invalid Latitude in TAB file : " + thisLat.ToString());
                        file.Close();
                        return false;
                    }

                    if (thisLong > 200)
                    {
                        MessageBox.Show("Invalid Longitude in TAB file : " + thisLong.ToString());
                        file.Close();
                        return false;
                    }                                        
                                        
                    if (thisInst.UTM_conversions.savedDatumIndex == 100)
                    {
                        UTM_datum thisDatum = new UTM_datum();
                        thisDatum.ShowDialog();
                        thisInst.UTM_conversions.savedDatumIndex = thisDatum.cbo_Datums.SelectedIndex;
                        thisInst.UTM_conversions.hemisphere = thisDatum.cboNorthOrSouth.SelectedItem.ToString();
                    }
                                                
                    UTM_conversion.UTM_coords theseUTMs = thisInst.UTM_conversions.LLtoUTM(thisLat, thisLong);
                    double thisUTMX = theseUTMs.UTMEasting;
                    double thisUTMY = theseUTMs.UTMNorthing;
                    
                    thisInst.txtUTMDatum.Text = thisInst.UTM_conversions.GetDatumString(thisInst.UTM_conversions.savedDatumIndex);
                    thisInst.txtUTMZone.Text = thisInst.UTM_conversions.UTMZoneNumber.ToString() + thisInst.UTM_conversions.hemisphere.Substring(0, 1);

                    if ((thisMetData.WS_units != "mph") && (thisMetData.WS_units != "mps"))
                    {
                        MessageBox.Show("WS units are not recognized. Expecting either 'mph or 'mps'.");
                        thisMetData.WS_units = "";
                        file.Close();
                        return false;
                    }

                    line = file.ReadLine();                    
                    line = line.Trim(',');
                    header = line.Split(delims);
                    Array.Resize(ref headerDeets, header.Length - 1); // minus one since not keeping time stamp header

                    for (int headInd = 1; headInd < header.Length; headInd++)
                    {
                        String thisHeader = header[headInd];
                        string sensorType = thisHeader.Substring(0, 4);
                        // check to see if Anem, Vane or temp
                        if ((sensorType == "Anem") || (sensorType == "Vane") || (sensorType == "Temp"))
                        {
                            headerDeets[headInd - 1].sensorType = sensorType;

                            int ind = thisHeader.IndexOf("_");
                            int secInd = 0;

                            int lastInd = thisHeader.LastIndexOf('_');

                            for (int i = ind + 1; i < thisHeader.Length; i++)
                                if (thisHeader[i] == '_')
                                {
                                    secInd = i;
                                    break;
                                }

                            int thisHeight = Convert.ToInt16(thisHeader.Substring(ind + 1, secInd - ind - 1));
                            headerDeets[headInd - 1].height = thisHeight;

                            if (sensorType == "Anem")
                            {
                                int thirdInd = 0;
                                for (int i = secInd + 1; i < thisHeader.Length; i++)
                                    if (thisHeader[i] == '_')
                                    {
                                        thirdInd = i;
                                        break;
                                    }

                                // read in orientation
                                lastInd = thisHeader.LastIndexOf("_");
                                int thisOrient = Convert.ToInt16(thisHeader.Substring(secInd + 1, lastInd - secInd - 1));
                                headerDeets[headInd - 1].orient = thisOrient;

                                // check to see if anem has been created already
                                bool alreadyGotIt = false;
                                char thisID = 'A';

                                foreach (Met_Data_Filter.Anem_Data thisAnem in thisMetData.anems)
                                {
                                    if ((thisAnem.height == thisHeight) && (thisAnem.orientation == thisOrient))
                                    {
                                        alreadyGotIt = true;
                                        headerDeets[headInd - 1].ID = thisAnem.ID;
                                        break;
                                    }
                                }

                                if (alreadyGotIt == false)
                                {
                                    foreach (Met_Data_Filter.Anem_Data thisAnem in thisMetData.anems)
                                    {
                                        if ((Math.Abs(thisAnem.height - thisHeight) < 2) && (thisAnem.ID == thisID))
                                            thisID = 'B';
                                    }

                                    int numAnems = thisMetData.GetNumAnems();
                                    Array.Resize(ref thisMetData.anems, numAnems + 1);
                                    thisMetData.anems[numAnems].height = thisHeight;
                                    thisMetData.anems[numAnems].orientation = thisOrient;
                                    thisMetData.anems[numAnems].ID = thisID;
                                    headerDeets[headInd - 1].ID = thisID;
                                }


                                headerDeets[headInd - 1].measType = thisHeader.Substring(lastInd + 1, thisHeader.Length - lastInd - 1);
                            }
                            else if ((thisHeader.Substring(0, 4) == "Vane"))
                            {
                                // check to see if vane has been created already
                                bool alreadyGotIt = false;

                                foreach (Met_Data_Filter.Vane_Data thisVane in thisMetData.vanes)
                                {
                                    if (thisVane.height == thisHeight)
                                    {
                                        alreadyGotIt = true;
                                        break;
                                    }
                                }

                                if (alreadyGotIt == false)
                                {
                                    Array.Resize(ref thisMetData.vanes, thisMetData.GetNumVanes() + 1);
                                    thisMetData.vanes[thisMetData.GetNumVanes() - 1].height = thisHeight;
                                }

                                headerDeets[headInd - 1].measType = thisHeader.Substring(lastInd + 1, thisHeader.Length - lastInd - 1);
                            }
                            else if (thisHeader.Substring(0, 4) == "Temp")
                            {
                                // check to see if vane has been created already
                                bool alreadyGotIt = false;

                                foreach (Met_Data_Filter.Temp_Data thisTemp in thisMetData.temps)
                                {
                                    if (thisTemp.height == thisHeight)
                                    {
                                        alreadyGotIt = true;
                                        break;
                                    }
                                }

                                if (alreadyGotIt == false)
                                {
                                    Array.Resize(ref thisMetData.temps, thisMetData.GetNumTemps() + 1);
                                    thisMetData.temps[thisMetData.GetNumTemps() - 1].height = thisHeight;
                                    thisMetData.temps[thisMetData.GetNumTemps() - 1].C_or_F = Convert.ToChar(thisHeader.Substring(lastInd + 1, thisHeader.Length - lastInd - 1));
                                }

                                headerDeets[headInd - 1].measType = thisHeader.Substring(secInd + 1, lastInd - secInd - 1);
                            }

                        }
                    }

                    // flag anemometers that don't have a redundant sensor (i.e. anem within 2 m)                    
                    for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                    {
                        thisMetData.anems[i].isOnlyMet = true;
                        for (int j = 0; j < thisMetData.GetNumAnems(); j++)
                            if ((i != j) && (Math.Abs(thisMetData.anems[i].height - thisMetData.anems[j].height) < 2))
                                thisMetData.anems[i].isOnlyMet = false;
                    }


                    // first read through data file and find Start and End dates
                    int dataCount = 0;
                    while ((line = file.ReadLine()) != null)
                    {
                        line = line.Trim(',');
                        String[] data = line.Split(delims);
                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        dataCount++;
                    }

                    // Size the arrays
                    for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                        Array.Resize(ref thisMetData.anems[i].windData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                        Array.Resize(ref thisMetData.vanes[i].dirData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                        Array.Resize(ref thisMetData.temps[i].temp, dataCount);


                    file.Close();
                    file = new StreamReader(filename);
                    line = file.ReadLine(); // Met name
                    line = file.ReadLine(); // WS units
                    line = file.ReadLine(); // header

                    // read in all data and fill anems, vanes and temps objects
                    dataCount = 0;

                    while ((line = file.ReadLine()) != null)
                    {
                        line = line.Trim(',');
                        String[] data = line.Split(delims);

                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        DateTime thisTimeStamp = Convert.ToDateTime(data[0]);
                        dataCount++;                                                

                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                            thisMetData.anems[i].windData[dataCount - 1].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                            thisMetData.vanes[i].dirData[dataCount - 1].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                            thisMetData.temps[i].temp[dataCount - 1].timeStamp = thisTimeStamp;

                        for (int i = 1; i < data.Length; i++)
                        {
                            String thisHeader = header[i];
                            String sensorType = thisHeader.Substring(0, 4);
                            double thisData = -999;

                            if (data[i] != "") thisData = Convert.ToDouble(data[i]);

                            if (sensorType == "Anem")
                            {
                                for (int j = 0; j < thisMetData.GetNumAnems(); j++)
                                    if ((thisMetData.anems[j].height == headerDeets[i - 1].height) && (thisMetData.anems[j].ID == headerDeets[i - 1].ID))
                                    {
                                        if (headerDeets[i - 1].measType == "Avg")
                                            thisMetData.anems[j].windData[dataCount - 1].avg = thisData;
                                        else if (headerDeets[i - 1].measType == "SD")
                                            thisMetData.anems[j].windData[dataCount - 1].SD = thisData;
                                        else if (headerDeets[i - 1].measType == "Min")
                                            thisMetData.anems[j].windData[dataCount - 1].min = thisData;
                                        else if (headerDeets[i - 1].measType == "Max")
                                            thisMetData.anems[j].windData[dataCount - 1].max = thisData;

                                        if ((thisData == -999) && (headerDeets[i - 1].measType == "Avg"))
                                            thisMetData.anems[j].windData[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.missing;
                                        else if (((thisMetData.WS_units == "mph") && ((thisData < 0) || (thisData > 80))) || ((thisMetData.WS_units == "mps") && ((thisData < 0) || (thisData > 40))))
                                            thisMetData.anems[j].windData[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.outsideRange;

                                        break;
                                    }
                            }

                            else if (sensorType == "Vane")
                            {
                                for (int j = 0; j < thisMetData.GetNumVanes(); j++)
                                    if (thisMetData.vanes[j].height == headerDeets[i - 1].height)
                                    {
                                        if (headerDeets[i - 1].measType == "Avg")
                                            thisMetData.vanes[j].dirData[dataCount - 1].avg = thisData;
                                        else if (headerDeets[i - 1].measType == "SD")
                                            thisMetData.vanes[j].dirData[dataCount - 1].SD = thisData;
                                        else if (headerDeets[i - 1].measType == "Min")
                                            thisMetData.vanes[j].dirData[dataCount - 1].min = thisData;
                                        else if (headerDeets[i - 1].measType == "Max")
                                            thisMetData.vanes[j].dirData[dataCount - 1].max = thisData;

                                        if ((thisData == -999) && (headerDeets[i - 1].measType == "Avg"))
                                            thisMetData.vanes[j].dirData[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.missing;
                                        else if (((thisData < 0) || (thisData > 360)) && (headerDeets[i - 1].measType == "Avg"))
                                            thisMetData.vanes[j].dirData[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.outsideRange;

                                        break;
                                    }
                            }
                            else if (sensorType == "Temp")
                            {
                                for (int j = 0; j < thisMetData.GetNumTemps(); j++)
                                    if (thisMetData.temps[j].height == headerDeets[i - 1].height)
                                    {
                                        if (headerDeets[i - 1].measType == "Avg")
                                            thisMetData.temps[j].temp[dataCount - 1].avg = thisData;
                                        else if (headerDeets[i - 1].measType == "SD")
                                            thisMetData.temps[j].temp[dataCount - 1].SD = thisData;
                                        else if (headerDeets[i - 1].measType == "Min")
                                            thisMetData.temps[j].temp[dataCount - 1].min = thisData;
                                        else if (headerDeets[i - 1].measType == "Max")
                                            thisMetData.temps[j].temp[dataCount - 1].max = thisData;

                                        if ((thisData == -999) && (headerDeets[i - 1].measType == "Avg"))
                                            thisMetData.temps[j].temp[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.missing;
                                        // check temp and flag if out of range
                                        else if (((thisMetData.temps[j].C_or_F == 'C') && ((thisMetData.temps[j].temp[dataCount - 1].avg < -50) || (thisMetData.temps[j].temp[dataCount - 1].avg > 50))) ||
                                            ((thisMetData.temps[j].C_or_F == 'F') && ((thisMetData.temps[j].temp[dataCount - 1].avg < -50) || (thisMetData.temps[j].temp[dataCount - 1].avg > 150))))
                                            thisMetData.temps[j].temp[dataCount - 1].filterFlag = Met_Data_Filter.Filter_Flags.outsideRange;

                                        break;
                                    }
                            }

                        }

                    }

                    if (thisMetData.GetNumAnems() > 0)
                        if (thisMetData.anems[0].windData.Length > 0)
                        {
                            thisMetData.allStartDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.allEndDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                            thisMetData.startDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.endDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                        }

                    if (thisMetData.WS_units == "mph") thisMetData.ConvertToMPS();

                    file.Close();

                    FilterExtrapolateAddMetTimeSeries(thisName, thisUTMX, thisUTMY, thisMetData, thisInst);
                }
                catch
                {
                    MessageBox.Show("Unable to import met time series data. Go to Met Data QC and click Generate Headers to create .csv with met data headers formatted for Continuum.", "Continuum 3");

                    thisMetData.anems = new Met_Data_Filter.Anem_Data[0];
                    thisMetData.vanes = new Met_Data_Filter.Vane_Data[0];
                    thisMetData.temps = new Met_Data_Filter.Temp_Data[0];
                    file.Close();
                    return false;
                }                
            }
            else
            {
                return false;
            }

            
            return true;
        }

        public struct windogSensor
        {
            public string sensorType; // Anem, Vane, Temperature, Tubulence
            public int height;
            public int index;
        }

        public bool ImportFilterExtrapMetDataWindographer(string filename, Continuum thisInst)
        {
            // Reads in formatted .csv file with anem, vane, and temp data. Adds the new met to metList. 
            // Filters (using all filters) and extrapolates to modeled height (specified on Input tab)
            // Returns true if met data successfully read in, filtered, extrapolated, and added
            //  NOT CURRENTLY USED YET. ONLY CONTINUUM FORMATTED FILES for 3.0

            Met_Data_Filter thisMetData = new Met_Data_Filter();
            String[] header;
            Header_Details[] headerDeets = new Header_Details[0];
            string line;
            Char[] delims = { ',', ' ' };
            Check_class check = new Check_class();
            int firstDataRow = 0;

            if (filename != "")
            {
                StreamReader file;
                try
                {
                    file = new StreamReader(filename);
                }
                catch
                {
                    MessageBox.Show("Error opening the met data file. Check that it's not open in another program.", "", MessageBoxButtons.OK);
                    return false;
                }
                int firstInd = filename.LastIndexOf('\\') + 1;
                int fileLength = filename.Length - filename.LastIndexOf('\\') - 1;
                string metName = filename.Substring(firstInd, fileLength);

                bool inputMet = check.CheckMetName(metName, thisInst.turbineList);

                if (inputMet == false)
                {
                    MessageBox.Show("Met site with same filename already imported", "Continuum 3.0");
                    return false;
                }                    
                                
                try
                {
                    // first read until it finds latitude and longitude
                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    string[] thisLine = line.Split(delims);
                    string firstIndex = "";

                    if (thisLine.Length > 0)
                        firstIndex = thisLine[0];
                    // First look for latitude
                    while (firstIndex != "Latitude" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        thisLine = line.Split(delims);

                        if (thisLine.Length > 0)
                            firstIndex = thisLine[0];
                    }
                    
                    if (firstIndex != "Latitude")
                    {
                        MessageBox.Show("Error reading file. Couldn't find Latitude", "Continuum 3.0");
                        return false;
                    }
                    
                    double thisLat = Convert.ToDouble(thisLine[thisLine.Length - 1]);

                    // Now look for longitude
                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    thisLine = line.Split(delims);

                    if (thisLine.Length > 0)
                        firstIndex = thisLine[0];

                    while (firstIndex != "Longitude" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        thisLine = line.Split(delims);

                        if (thisLine.Length > 0)
                            firstIndex = thisLine[0];
                    }

                    if (firstIndex != "Longitude")
                    {
                        MessageBox.Show("Error reading file. Couldn't find Longitude", "Continuum 3.0");
                        return false;
                    }

                    double thisLong = Convert.ToDouble(thisLine[thisLine.Length - 1]);
                                   
                    // Check if UTM datum has been defined, if not, ask user
                    if (thisInst.UTM_conversions.savedDatumIndex == 100)
                    {
                        UTM_datum thisDatum = new UTM_datum();
                        thisDatum.ShowDialog();
                        thisInst.UTM_conversions.savedDatumIndex = thisDatum.cbo_Datums.SelectedIndex;
                        thisInst.UTM_conversions.hemisphere = thisDatum.cboNorthOrSouth.SelectedItem.ToString();
                    }

                    UTM_conversion.UTM_coords theseUTMs = thisInst.UTM_conversions.LLtoUTM(thisLat, thisLong);
                    double thisUTMX = theseUTMs.UTMEasting;
                    double thisUTMY = theseUTMs.UTMNorthing;

                    thisInst.txtUTMDatum.Text = thisInst.UTM_conversions.GetDatumString(thisInst.UTM_conversions.savedDatumIndex);
                    thisInst.txtUTMZone.Text = thisInst.UTM_conversions.UTMZoneNumber.ToString() + thisInst.UTM_conversions.hemisphere.Substring(0, 1);
                    
                    // Read file until it gets to header (starts with Date/Time)

                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    header = line.Split(delims);

                    if (header.Length > 0)
                        firstIndex = header[0];

                    while (firstIndex != "Date/Time" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        header = line.Split(delims);

                        if (header.Length > 0)
                            firstIndex = header[0];
                    }

                    if (firstIndex != "Date/Time")
                    {
                        MessageBox.Show("Error reading file. Couldn't find row with headers", "Continuum 3.0");
                        return false;
                    }

                    windogSensor[] sensorIndices = new windogSensor[0];
                    int sensorCount = 0;

                    for (int headInd = 0; headInd < header.Length; headInd++)
                    {
                        string thisHeader = header[headInd];

                        // check to see if Anem, Vane or temp
                        if (thisHeader == "Speed" || thisHeader == "Direction" || thisHeader == "Temperature" || thisHeader == "Density" || thisHeader == "Intensity")
                        {
                            // the next header will be the height with 'm'
                            int unitInd = header[headInd + 1].LastIndexOf('m');
                            int headerLen = header[headInd + 1].Length;
                            int thisHeight = Convert.ToInt16(header[headInd + 1].Substring(0, unitInd));

                            sensorCount++;
                            Array.Resize(ref sensorIndices, sensorCount);
                            sensorIndices[sensorCount - 1].height = thisHeight;
                            sensorIndices[sensorCount - 1].index = sensorCount;
                            sensorIndices[sensorCount - 1].sensorType = thisHeader;

                            if (thisHeader == "Speed")
                            {
                                int numAnems = thisMetData.GetNumAnems();
                                Array.Resize(ref thisMetData.anems, numAnems + 1);
                                thisMetData.anems[numAnems].height = thisHeight;
                            }
                            else if (thisHeader == "Direction")
                            {
                                int numVanes = thisMetData.GetNumVanes();
                                Array.Resize(ref thisMetData.vanes, numVanes + 1);
                                thisMetData.vanes[numVanes].height = thisHeight;
                            }
                            else if (thisHeader == "Temperature")
                            {
                                int numTemps = thisMetData.GetNumTemps();
                                Array.Resize(ref thisMetData.temps, numTemps + 1);
                                thisMetData.temps[numTemps].height = thisHeight;
                            }                           

                        }
                    }                          
                     
                    // first read through data file and find Start and End dates
                    int dataCount = 0;
                    while ((line = file.ReadLine()) != null)
                    {
                        String[] data = line.Split(delims);
                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        dataCount++;
                    }

                    // Size the arrays
                    for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                        Array.Resize(ref thisMetData.anems[i].windData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                        Array.Resize(ref thisMetData.vanes[i].dirData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                        Array.Resize(ref thisMetData.temps[i].temp, dataCount);


                    file.Close();
                    file = new StreamReader(filename);

                    for (int i = 0; i < firstDataRow; i++)
                        file.ReadLine();

                    // read in all data and fill anems, vanes and temps objects
                    dataCount = 0;

                    while ((line = file.ReadLine()) != null)
                    {
                        string[] data = line.Split(delims);

                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        int thisYear = Convert.ToInt16(data[0]);
                        int thisMonth = Convert.ToInt16(data[1]);
                        int thisDay = Convert.ToInt16(data[2]);
                        int thisHour = Convert.ToInt16(data[3].Substring(0, 2));
                        int thisMinute = Convert.ToInt16(data[3].Substring(2, 2));

                        DateTime thisTimeStamp = new DateTime(thisYear, thisMonth, thisDay, thisHour, 0, 0);

                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                            thisMetData.anems[i].windData[dataCount].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                            thisMetData.vanes[i].dirData[dataCount].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                            thisMetData.temps[i].temp[dataCount].timeStamp = thisTimeStamp;

                        for (int ind = 4; ind < data.Length; ind++)
                        {
                            for (int sensorInd = 0; sensorInd < sensorCount; sensorInd++)
                            {
                                if (sensorIndices[sensorInd].index == (ind - 3)) // Data starts after time stamp which takes up first four indices
                                {
                                    if (sensorIndices[sensorInd].sensorType == "Speed")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                                        {
                                            if (thisMetData.anems[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.anems[i].windData[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.anems[i].windData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Direction")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                                        {
                                            if (thisMetData.vanes[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.vanes[i].dirData[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.vanes[i].dirData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Temperature")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                                        {
                                            if (thisMetData.temps[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.temps[i].temp[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.temps[i].temp[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Intensity")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                                        {
                                            if (thisMetData.anems[i].height == sensorIndices[sensorInd].height)
                                            {
                                                // Convert TI into SD
                                                double thisSD = thisMetData.anems[i].windData[dataCount].avg * Convert.ToDouble(data[ind]);

                                                thisMetData.anems[i].windData[dataCount].SD = thisSD;
                                                thisMetData.anems[i].windData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        dataCount++;
                    }                     
                                        
                    if (thisMetData.GetNumAnems() > 0)
                        if (thisMetData.anems[0].windData.Length > 0)
                        {
                            thisMetData.allStartDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.allEndDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                            thisMetData.startDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.endDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                        }
                                        
                    file.Close();

                    ExtrapolateAddMetTimeSeries(metName, thisUTMX, thisUTMY, thisMetData, thisInst);
                }
                catch
                {
                    thisMetData.anems = new Met_Data_Filter.Anem_Data[0];
                    thisMetData.vanes = new Met_Data_Filter.Vane_Data[0];
                    thisMetData.temps = new Met_Data_Filter.Temp_Data[0];
                    file.Close();
                    return false;
                }
            }
            else
            {
                return false;
            }


            return true;
        }

        public bool ImportFilterExtrapMetDataSymphonie(string filename, Continuum thisInst)
        {
            // Reads in raw text file generated by Symphonie logger. Adds the new met to metList. 
            // Filters (using all filters) and extrapolates to modeled height (specified on Input tab)
            // Returns true if met data successfully read in, filtered, extrapolated, and added
            // NOT WORKING YET

            Met_Data_Filter thisMetData = new Met_Data_Filter();
            String[] header;            
            string line;
            Char[] delims = { ',', ' ' };
            Check_class check = new Check_class();
            int firstDataRow = 0;

            if (filename != "")
            {
                StreamReader file;
                try
                {
                    file = new StreamReader(filename);
                }
                catch
                {
                    MessageBox.Show("Error opening the met data file. Check that it's not open in another program.", "", MessageBoxButtons.OK);
                    return false;
                }
                int firstInd = filename.LastIndexOf('\\') + 1;
                int fileLength = filename.Length - filename.LastIndexOf('\\') - 1;
                string metName = filename.Substring(firstInd, fileLength);

                bool inputMet = check.CheckMetName(metName, thisInst.turbineList);

                if (inputMet == false)
                {
                    MessageBox.Show("Met site with same filename already imported", "Continuum 3.0");
                    return false;
                }

                try
                {
                    // first read until it finds latitude and longitude
                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    string[] thisLine = line.Split(delims);
                    string firstIndex = "";

                    if (thisLine.Length > 0)
                        firstIndex = thisLine[0];
                    // First look for latitude
                    while (firstIndex != "Latitude" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        thisLine = line.Split(delims);

                        if (thisLine.Length > 0)
                            firstIndex = thisLine[0];
                    }

                    if (firstIndex != "Latitude")
                    {
                        MessageBox.Show("Error reading file. Couldn't find Latitude", "Continuum 3.0");
                        return false;
                    }

                    double thisLat = Convert.ToDouble(thisLine[thisLine.Length - 1]);

                    // Now look for longitude
                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    thisLine = line.Split(delims);

                    if (thisLine.Length > 0)
                        firstIndex = thisLine[0];

                    while (firstIndex != "Longitude" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        thisLine = line.Split(delims);

                        if (thisLine.Length > 0)
                            firstIndex = thisLine[0];
                    }

                    if (firstIndex != "Longitude")
                    {
                        MessageBox.Show("Error reading file. Couldn't find Longitude", "Continuum 3.0");
                        return false;
                    }

                    double thisLong = Convert.ToDouble(thisLine[thisLine.Length - 1]);

                    // Check if UTM datum has been defined, if not, ask user
                    if (thisInst.UTM_conversions.savedDatumIndex == 100)
                    {
                        UTM_datum thisDatum = new UTM_datum();
                        thisDatum.ShowDialog();
                        thisInst.UTM_conversions.savedDatumIndex = thisDatum.cbo_Datums.SelectedIndex;
                        thisInst.UTM_conversions.hemisphere = thisDatum.cboNorthOrSouth.SelectedItem.ToString();
                    }

                    UTM_conversion.UTM_coords theseUTMs = thisInst.UTM_conversions.LLtoUTM(thisLat, thisLong);
                    double thisUTMX = theseUTMs.UTMEasting;
                    double thisUTMY = theseUTMs.UTMNorthing;

                    thisInst.txtUTMDatum.Text = thisInst.UTM_conversions.GetDatumString(thisInst.UTM_conversions.savedDatumIndex);
                    thisInst.txtUTMZone.Text = thisInst.UTM_conversions.UTMZoneNumber.ToString() + thisInst.UTM_conversions.hemisphere.Substring(0, 1);

                    // Read file until it gets to header (starts with Date/Time)

                    line = file.ReadLine();
                    firstDataRow++;
                    line.Trim();
                    header = line.Split(delims);

                    if (header.Length > 0)
                        firstIndex = header[0];

                    while (firstIndex != "Date/Time" && file.EndOfStream == false)
                    {
                        line = file.ReadLine();
                        firstDataRow++;
                        line.Trim();
                        header = line.Split(delims);

                        if (header.Length > 0)
                            firstIndex = header[0];
                    }

                    if (firstIndex != "Date/Time")
                    {
                        MessageBox.Show("Error reading file. Couldn't find row with headers", "Continuum 3.0");
                        return false;
                    }

                    windogSensor[] sensorIndices = new windogSensor[0];
                    int sensorCount = 0;

                    for (int headInd = 0; headInd < header.Length; headInd++)
                    {
                        string thisHeader = header[headInd];

                        // check to see if Anem, Vane or temp
                        if (thisHeader == "Speed" || thisHeader == "Direction" || thisHeader == "Temperature" || thisHeader == "Density" || thisHeader == "Intensity")
                        {
                            // the next header will be the height with 'm'
                            int unitInd = header[headInd + 1].LastIndexOf('m');
                            int headerLen = header[headInd + 1].Length;
                            int thisHeight = Convert.ToInt16(header[headInd + 1].Substring(0, unitInd));

                            sensorCount++;
                            Array.Resize(ref sensorIndices, sensorCount);
                            sensorIndices[sensorCount - 1].height = thisHeight;
                            sensorIndices[sensorCount - 1].index = sensorCount;
                            sensorIndices[sensorCount - 1].sensorType = thisHeader;

                            if (thisHeader == "Speed")
                            {
                                int numAnems = thisMetData.GetNumAnems();
                                Array.Resize(ref thisMetData.anems, numAnems + 1);
                                thisMetData.anems[numAnems].height = thisHeight;
                            }
                            else if (thisHeader == "Direction")
                            {
                                int numVanes = thisMetData.GetNumVanes();
                                Array.Resize(ref thisMetData.vanes, numVanes + 1);
                                thisMetData.vanes[numVanes].height = thisHeight;
                            }
                            else if (thisHeader == "Temperature")
                            {
                                int numTemps = thisMetData.GetNumTemps();
                                Array.Resize(ref thisMetData.temps, numTemps + 1);
                                thisMetData.temps[numTemps].height = thisHeight;
                            }

                        }
                    }

                    // first read through data file and find Start and End dates
                    int dataCount = 0;
                    while ((line = file.ReadLine()) != null)
                    {
                        String[] data = line.Split(delims);
                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        dataCount++;
                    }

                    // Size the arrays
                    for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                        Array.Resize(ref thisMetData.anems[i].windData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                        Array.Resize(ref thisMetData.vanes[i].dirData, dataCount);

                    for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                        Array.Resize(ref thisMetData.temps[i].temp, dataCount);


                    file.Close();
                    file = new StreamReader(filename);

                    for (int i = 0; i < firstDataRow; i++)
                        file.ReadLine();

                    // read in all data and fill anems, vanes and temps objects
                    dataCount = 0;

                    while ((line = file.ReadLine()) != null)
                    {
                        string[] data = line.Split(delims);

                        if (data.Length > 0)
                            if (data[0] == "")
                                break;

                        int thisYear = Convert.ToInt16(data[0]);
                        int thisMonth = Convert.ToInt16(data[1]);
                        int thisDay = Convert.ToInt16(data[2]);
                        int thisHour = Convert.ToInt16(data[3].Substring(0, 2));
                        int thisMinute = Convert.ToInt16(data[3].Substring(2, 2));

                        DateTime thisTimeStamp = new DateTime(thisYear, thisMonth, thisDay, thisHour, 0, 0);

                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                            thisMetData.anems[i].windData[dataCount].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                            thisMetData.vanes[i].dirData[dataCount].timeStamp = thisTimeStamp;

                        for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                            thisMetData.temps[i].temp[dataCount].timeStamp = thisTimeStamp;

                        for (int ind = 4; ind < data.Length; ind++)
                        {
                            for (int sensorInd = 0; sensorInd < sensorCount; sensorInd++)
                            {
                                if (sensorIndices[sensorInd].index == (ind - 3)) // Data starts after time stamp which takes up first four indices
                                {
                                    if (sensorIndices[sensorInd].sensorType == "Speed")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                                        {
                                            if (thisMetData.anems[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.anems[i].windData[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.anems[i].windData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Direction")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumVanes(); i++)
                                        {
                                            if (thisMetData.vanes[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.vanes[i].dirData[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.vanes[i].dirData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Temperature")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumTemps(); i++)
                                        {
                                            if (thisMetData.temps[i].height == sensorIndices[sensorInd].height)
                                            {
                                                thisMetData.temps[i].temp[dataCount].avg = Convert.ToDouble(data[ind]);
                                                thisMetData.temps[i].temp[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    else if (sensorIndices[sensorInd].sensorType == "Intensity")
                                    {
                                        for (int i = 0; i < thisMetData.GetNumAnems(); i++)
                                        {
                                            if (thisMetData.anems[i].height == sensorIndices[sensorInd].height)
                                            {
                                                // Convert TI into SD
                                                double thisSD = thisMetData.anems[i].windData[dataCount].avg * Convert.ToDouble(data[ind]);

                                                thisMetData.anems[i].windData[dataCount].SD = thisSD;
                                                thisMetData.anems[i].windData[dataCount].filterFlag = Met_Data_Filter.Filter_Flags.Valid;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        dataCount++;
                    }

                    if (thisMetData.GetNumAnems() > 0)
                        if (thisMetData.anems[0].windData.Length > 0)
                        {
                            thisMetData.allStartDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.allEndDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                            thisMetData.startDate = thisMetData.anems[0].windData[0].timeStamp;
                            thisMetData.endDate = thisMetData.anems[0].windData[thisMetData.anems[0].windData.Length - 1].timeStamp;
                        }

                    file.Close();

                    ExtrapolateAddMetTimeSeries(metName, thisUTMX, thisUTMY, thisMetData, thisInst);
                }
                catch
                {
                    thisMetData.anems = new Met_Data_Filter.Anem_Data[0];
                    thisMetData.vanes = new Met_Data_Filter.Vane_Data[0];
                    thisMetData.temps = new Met_Data_Filter.Temp_Data[0];
                    file.Close();
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }


        public void RunMCP(ref Met thisMet, MERRA thisMERRA, Continuum thisInst, string MCP_method)
        {            
            thisMet.mcp = new MCP();
            thisMet.mcp.New_MCP(true, true, thisInst); // reads the MCP settings from MCP tab

            // Get MERRA data as the reference data
            thisMet.mcp.refData = thisMet.mcp.GetRefData(thisMERRA, ref thisMet, thisInst);

            // Get extrapolated met dat as the target data
            thisMet.mcp.targetData = thisMet.mcp.GetTargetData(thisInst.modeledHeight, thisMet);

       //     thisMet.metData.FindStartEndDatesWithMaxRecovery();            
            thisMet.mcp.FindConcurrentData(thisMet.metData.startDate, thisMet.metData.endDate);
            
            if (thisMet.mcp.gotConc == true)
                thisMet.mcp.DoMCP(thisMet.mcp.concStart, thisMet.mcp.concEnd, true, MCP_method, thisInst, thisMet);                                   
            
        }

        public void AddAllMetDataToDBAndClear(Continuum thisInst)
        {
            if (thisInst.metList.isTimeSeries == false)
                return;

            for (int i = 0; i < ThisCount; i++)
            {
                metItem[i].metData.AddSensorDatatoDBAndClear(thisInst, metItem[i].name);
                metItem[i].metData.ClearAlphaAndSimulatedEstimates();
            }               

        }

        public void ClearMCPRefTargetConcLTEstData()
        {
            // Clears all MCP reference, target, concurrent and LT estimate data. Not saved in file. It's regenerated as needed
            
            for (int i = 0; i < ThisCount; i++)
            {
                if (metItem[i].mcp != null)
                {
                    metItem[i].mcp.refData = new MCP.Site_data[0];
                    metItem[i].mcp.targetData = new MCP.Site_data[0];
                    metItem[i].mcp.concData = new MCP.Concurrent_data[0];
                    metItem[i].mcp.LT_WS_Ests = new MCP.Site_data[0];
                    metItem[i].mcp.gotConc = false;
                    metItem[i].mcp.gotRef = false;
                    metItem[i].mcp.gotTarg = false;
                }
            }
        }

        public int GetWD_Ind(double thisWD)
        {
            int WD_Ind = 0;

            if (thisWD == -999)
                WD_Ind = -999;
            else
            {
                WD_Ind = (int)Math.Round(thisWD / (360 / (double)numWD), 0, MidpointRounding.AwayFromZero);
                if (WD_Ind == numWD) WD_Ind = 0;
            }

            return WD_Ind;
        }

        public Met GetClosestMet(double targetX, double targetY)
        {
            // Finds and returns Met site closest to target UTMX/Y
            Met closestMet = new Met();
            double minDist = 1000000;
            TopoInfo topo = new TopoInfo(); // Created for CalcDistanceBetweenTwoPoints function

            for (int i = 0; i < ThisCount; i++)
            {
                double thisDist = topo.CalcDistanceBetweenPoints(metItem[i].UTMX, metItem[i].UTMY, targetX, targetY);

                if (thisDist < minDist)
                {
                    closestMet = metItem[i];
                    minDist = thisDist;
                }
            }

            return closestMet;
        }

        public void AreAllMetsMCPd()
        {
            // Checks if all met sites have MCP calcs (time series model only) and sees allMCPd flag
            allMCPd = true;

            for (int i = 0; i < ThisCount; i++)
            {
                if (metItem[i].mcp == null)
                    allMCPd = false;
                else if (metItem[i].mcp.gotMCP_Est == false)
                    allMCPd = false;
            }

        }

        public void ResetMetParams()
        {
            // Resets all mets' exposure, grid stats, turbulence, and WSWD_Dists. This is called when the numWD is changed on MCP tab
            
            for (int i = 0; i < ThisCount; i++)
            {
                metItem[i].expo = null;
                metItem[i].gridStats = new Grid_Info();
                metItem[i].turbulence = new Met.Turbulence();
                metItem[i].WSWD_Dists = null;
            }

            expoIsCalc = false;
        }
                
    }
}
