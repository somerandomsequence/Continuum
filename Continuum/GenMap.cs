﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ContinuumNS
{
    public partial class GenMap : Form
    {
        public GenMap(Continuum continuum)
        {
            InitializeComponent();
            thisInst = continuum;

            if (thisInst.topo.useSepMod)
            {
                txtMap_FlowSep_Used.Text = "Flow Sep. model used";
                txtMap_FlowSep_Used.BackColor = Color.LightBlue;
            }
            else
            {
                txtMap_FlowSep_Used.Text = "Flow Sep. model NOT used";
                txtMap_FlowSep_Used.BackColor = Color.LightCoral;
            }

            if (thisInst.topo.useSR)
            {
                txtMap_LC_used.Text = "Roughness model used";
                txtMap_LC_used.BackColor = Color.MediumSeaGreen;
            }
            else
            {
                txtMap_LC_used.Text = "Roughness model NOT used";
                txtMap_LC_used.BackColor = Color.LightCoral;
            }

            thisInst.metList.AreAllMetsMCPd();
            if (thisInst.metList.allMCPd)
            {
                txtisMCPdGenMap.Text = "MCP'd Met data used";
                txtisMCPdGenMap.BackColor = Color.MediumOrchid;
            }
            else
            {
                txtisMCPdGenMap.Text = "Meas Met data used";
                txtisMCPdGenMap.BackColor = Color.LightCoral;
            }

        }

        public int minUTMX;   // Minimum UTM X coordinate
        public int maxUTMX;   // Maximum UTM X coordinate
        public int minUTMY;   // Minimum UTM Y coordinate
        public int maxUTMY;   // Maximum UTM Y coordinate
        public int gridReso;   // Grid resolution
        public int numGrid;   // Number of grid points
        string mapName = ""; // Map name
        public int numX;   // Number of grid points along X
        public int numY;   // Number of grid points along Y
        public int minUTMX_Limit; // Minimum allowable X
        public int minUTMY_Limit; // Minimum allowable Y
        public int maxUTMX_Limit; // Maximum allowable X
        public int maxUTMY_Limit; // Maximum allowable Y
        int whatToMap = 0; // Index of selected map type
        string powerCurve = ""; // Name of power curve used in map (if AEP map)
        Met[] metsUsed;
        Model[] models; // Wind flow model parameters
        Continuum thisInst;
        bool useTimeSeries;


        private void btnCancel_Click(object sender, EventArgs e) {
            Close();
        }

        public void UpdateTextboxes()
        {
            txtMinUTMX.Text = minUTMX.ToString();
            txtMinUTMY.Text = minUTMY.ToString();
            txtMaxUTMX.Text = maxUTMX.ToString();
            txtMaxUTMY.Text = maxUTMY.ToString();
            txtNumPoints.Text = numGrid.ToString();
        }

        public void UpdateLimits()
        {
            if (gridReso == 0)
                UpdateGridReso();

            int minX_Lim = (int)thisInst.topo.topoNumXY.X.all.min + 12000;
            int minY_Lim = (int)thisInst.topo.topoNumXY.Y.all.min + 12000;
            int maxX_Lim = (int)thisInst.topo.topoNumXY.X.all.max - 12000;
            int maxY_Lim = (int)thisInst.topo.topoNumXY.Y.all.max - 12000;
            
            int minX_LimInd = (int)Math.Ceiling((minX_Lim - thisInst.topo.topoNumXY.X.all.min) / gridReso);
            int minY_LimInd = (int)Math.Ceiling((minY_Lim - thisInst.topo.topoNumXY.Y.all.min) / gridReso);
            int maxX_LimInd = (int)Math.Floor((maxX_Lim - thisInst.topo.topoNumXY.X.all.min) / gridReso);
            int maxY_LimInd = (int)Math.Floor((maxY_Lim - thisInst.topo.topoNumXY.Y.all.min) / gridReso);

            minUTMX_Limit = (int)thisInst.topo.topoNumXY.X.all.min + minX_LimInd * gridReso;
            minUTMY_Limit = (int)thisInst.topo.topoNumXY.Y.all.min + minY_LimInd * gridReso;
            maxUTMX_Limit = (int)thisInst.topo.topoNumXY.X.all.min + maxX_LimInd * gridReso;
            maxUTMY_Limit = (int)thisInst.topo.topoNumXY.Y.all.min + maxY_LimInd * gridReso;

            if (minUTMX < minUTMX_Limit || minUTMX == 0) minUTMX = minUTMX_Limit;
            if (minUTMY < minUTMY_Limit || minUTMY == 0) minUTMY = minUTMY_Limit;
            if (maxUTMX > maxUTMX_Limit || maxUTMX == 0) maxUTMX = maxUTMX_Limit;
            if (maxUTMY > maxUTMY_Limit || maxUTMY == 0) maxUTMY = maxUTMY_Limit;

            UpdateNumPoints();
        }

        public void GetBiggestArea()
        {
            minUTMX = minUTMX_Limit;
            maxUTMX = maxUTMX_Limit;
            minUTMY = minUTMY_Limit;
            maxUTMY = maxUTMY_Limit;
            UpdateNumPoints();
        }

        public void UpdateGridReso()
        {
            try
            {
                gridReso = Convert.ToInt16(txtMapReso.Text);
            }
            catch
            {
                MessageBox.Show("Invalid grid resolution.", "Continuum 3");
                txtMapReso.Text = gridReso.ToString();
                return;
            }
        }

        public void UpdateNumPoints()
        {
            if (gridReso != 0)
            {
                numX = (maxUTMX - minUTMX) / gridReso + 1;
                numY = (maxUTMY - minUTMY) / gridReso + 1;
                numGrid = numX * numY;
            }
            else
                numGrid = 0;

            txtNumPoints.Text = numGrid.ToString();
        }
               
        
        private void GetMapSettings()
        {            
            double height = thisInst.modeledHeight;
            
            try
            {
                whatToMap = cboWhatToMap.SelectedIndex;
            }
            catch 
            {
                MessageBox.Show("Select parameter to map.", "Continuum 3");
                return;
            }                        

            bool isCalibrated = false;
            if (thisInst.metList.ThisCount > 1)
                isCalibrated = true;

            Map thisMap = null;
            // 0 = UW map, 1 = DW map, 2 = WS map (site-calibrated), 3 = Gross AEP map (site-calibrated), 4 = WS map (default), 5 = Gross AEP map (default)
            if (whatToMap == 0)
                mapName = "Upwind Exposure";
            else if (whatToMap == 1)
                mapName = "Downwind Exposure";
            else if (whatToMap == 2)
            {
                mapName = height + " m Wind Speed";
                if (isCalibrated == false) whatToMap = 4;
            }
            else if (whatToMap == 3)
            {
                try
                {
                    powerCurve = cboPowerCrvs.SelectedItem.ToString();
                }
                catch 
                {
                    MessageBox.Show("No power Curves imported.  Cannot create map of Gross AEP.", "Continuum 3");
                    return;
                }
                mapName = "Gross AEP with " + powerCurve;
                if (isCalibrated == false) whatToMap = 5;
            }

            int mapInd = 1;

            // Check to see if there are other maps of same type
            for (int i = 0; i < thisInst.mapList.ThisCount; i++)
            {
                thisMap = thisInst.mapList.mapItem[i];

                if (thisMap.modelType == whatToMap)
                    mapInd++;
            }

            if (mapInd > 1)
            {
                if (whatToMap >= 2)
                {
                    if (isCalibrated == false)
                        mapName = mapName + " " + mapInd + " using default model";
                    else
                        mapName = mapName + " " + mapInd + " using site-calibrated model";
                }
                else
                {                    
                    mapName = mapName + " " + mapInd;
                }
            }
            else if (whatToMap >= 2)
            {
                if (isCalibrated == false)
                    mapName = mapName + " using default model";
                else
                    mapName = mapName + " using site-calibrated model";
            }

            // Check that map name hasn't been taken
            bool mapNameTaken = false;
            for (int i = 0; i < thisInst.mapList.ThisCount; i++)
                if (thisInst.mapList.mapItem[i].mapName == mapName)
                    mapNameTaken = true;

            while (mapNameTaken == true)
            {
                mapInd++;
                if (whatToMap >= 2)
                {
                    if (isCalibrated == false)
                        mapName = height + " m Wind Speed " + mapInd + " using default model";
                    else
                        mapName = height + " m Wind Speed " + mapInd + " using site-calibrated model";
                }
                else
                {                    
                    mapName = mapName + " " + mapInd;
                }

                mapNameTaken = false;
                for (int i = 0; i < thisInst.mapList.ThisCount; i++)
                    if (thisInst.mapList.mapItem[i].mapName == mapName)
                        mapNameTaken = true;

            }

            if (metsUsed == null)
                metsUsed = new Met[0];

            for(int i = 0; i < chkMetsToUse.CheckedItems.Count; i++)
            {
                for (int j = 0; j < thisInst.metList.ThisCount; j++)
                {
                    if (chkMetsToUse.CheckedItems[i].ToString() == thisInst.metList.metItem[j].name)
                    {
                        int newCount = metsUsed.Length + 1;
                        Array.Resize(ref metsUsed, newCount);
                        metsUsed[newCount - 1] = thisInst.metList.metItem[j];
                    }
                }
            }

            if (cboUseTimeSeries.SelectedItem.ToString() == "Use Time Series")
                useTimeSeries = true;
            else
                useTimeSeries = false;
            
            if (thisInst.metList.isTimeSeries == false || thisInst.metList.isMCPd == false || useTimeSeries == false)
                models = thisInst.modelList.GetModels(thisInst, thisInst.metList.GetMetsUsed(), 4000, 10000, isCalibrated, Met.TOD.All, Met.Season.All, thisInst.modeledHeight,false);                     
            
        }
                
        
        private void btnMinMax_Click(object sender, EventArgs e)
        {
            // Finds the largest area that can be mapped based on loaded topo data and updates Min/Max X/Y

            UpdateLimits();
            GetBiggestArea();
            UpdateTextboxes(); 
            
        }
        
        private void btnCoordsAllTurbs_Click(object sender, EventArgs e)
        {
            // Finds map area that covers all loaded turbine sites and updates Min/Max X/Y
            
            double turbMinX = 0;
            double turbMinY = 0;
            double turbMaxX = 0;
            double turbMaxY = 0;            
            int numTurbines = thisInst.turbineList.TurbineCount;

            if (numTurbines == 0) {
                MessageBox.Show("No Turbine sites have been loaded.", "Continuum 3");
                return;
            }

            for (int i = 0; i < numTurbines; i++) {

                Turbine thisTurb = thisInst.turbineList.turbineEsts[i];
                if (i == 0) {
                    turbMinX = thisTurb.UTMX;
                    turbMinY = thisTurb.UTMY;
                    turbMaxX = thisTurb.UTMX;
                    turbMaxY = thisTurb.UTMY;
                }

                if (thisTurb.UTMX < turbMinX) turbMinX = thisTurb.UTMX;
                if (thisTurb.UTMY < turbMinY) turbMinY = thisTurb.UTMY;
                if (thisTurb.UTMX > turbMaxX) turbMaxX = thisTurb.UTMX;
                if (thisTurb.UTMY > turbMaxY) turbMaxY = thisTurb.UTMY;
            }

            // Make a buffer around turbines (maximum of 500 m or grid resolution 
            turbMinX = turbMinX - Math.Max(500, gridReso);
            turbMinY = turbMinY - Math.Max(500, gridReso);
            turbMaxX = turbMaxX + Math.Max(500, gridReso);
            turbMaxY = turbMaxY + Math.Max(500, gridReso);

            if (turbMinX > minUTMX) minUTMX = (int)Math.Round(turbMinX, 0);
            if (turbMinY > minUTMY) minUTMY = (int)Math.Round(turbMinY, 0);
            if (turbMaxX < maxUTMX) maxUTMX = (int)Math.Round(turbMaxX, 0);
            if (turbMaxY < maxUTMY) maxUTMY = (int)Math.Round(turbMaxY, 0);

            UpdateNumPoints();
            UpdateTextboxes();
        }
        
        private void cboWhatToMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            // if ( a Gross AEP map is selected from dropbown, the power curve dropdown is updated
            int selectedInd = 0;
             
            try {
                selectedInd = cboWhatToMap.SelectedIndex;
            }
            catch  {
                return;
            }

            if (selectedInd == 3) { // fill power curves
                int numPowerCurves = thisInst.turbineList.PowerCurveCount;

                if (numPowerCurves == 0)
                    cboPowerCrvs.Items.Clear();
                else {

                    for (int i = 0; i < numPowerCurves; i++) {
                        TurbineCollection.PowerCurve This_Pwr_Crv = thisInst.turbineList.powerCurves[i];
                        cboPowerCrvs.Items.Add(This_Pwr_Crv.name);
                    }
                }

                try {
                    cboPowerCrvs.SelectedIndex = 0;
                }
                catch  {
                    return;
                }
            }
            else {
                cboPowerCrvs.Text = "";
                cboPowerCrvs.Items.Clear();
            }
        }


        private void txtMapReso_TextChanged(object sender, EventArgs e)
        {
            // Recalculates number of grid points and updates txtNumPoints
            UpdateGridReso();
            UpdateLimits();
            UpdateNumPoints();
            UpdateTextboxes();
            
        }

        private void txtMinUTMX_TextChanged(object sender, EventArgs e)
        {            
            try
            {
                if (txtMinUTMX.TextLength < 6)
                    return;

                minUTMX = Convert.ToInt32(txtMinUTMX.Text);
                if (minUTMX < minUTMX_Limit)
                {
                    MessageBox.Show("Entered Min UTMX is less than minimum allowed value. Resetting to minimum.", "Continuum 3");
                    minUTMX = minUTMX_Limit;
                }

                UpdateNumPoints();
                UpdateTextboxes();
                
            }
            catch { }
        }

        private void txtMaxUTMX_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (txtMaxUTMX.TextLength < 6)
                    return;

                maxUTMX = Convert.ToInt32(txtMaxUTMX.Text);
                if (maxUTMX > maxUTMX_Limit)
                {
                    MessageBox.Show("Entered Max UTMX is more than maximum allowed value. Resetting to maximum.", "Continuum 3");
                    maxUTMX = maxUTMX_Limit;
                }

                UpdateNumPoints();
                UpdateTextboxes();
            }
            catch { }
        }

        private void txtMinUTMY_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (txtMinUTMY.TextLength < 7)
                    return;

                minUTMY = Convert.ToInt32(txtMinUTMY.Text);
                if (minUTMY < minUTMY_Limit)
                {
                    MessageBox.Show("Entered Min UTMY is less than minimum allowed value. Resetting to minimum.", "Continuum 3");
                    minUTMY = minUTMY_Limit;
                }

                UpdateNumPoints();
                UpdateTextboxes();
            }
            catch { }
        }

        private void txtMaxUTMY_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (txtMaxUTMY.TextLength < 7)
                    return;

                maxUTMY = Convert.ToInt32(txtMaxUTMY.Text);
                if (maxUTMY > maxUTMY_Limit)
                {
                    MessageBox.Show("Entered Max UTMY is more than maximum allowed value. Resetting to maximum.", "Continuum 3");
                    maxUTMY = maxUTMY_Limit;
                }

                UpdateNumPoints();
                UpdateTextboxes();
            }
            catch { }
        }

        private void btnGenMap_Click(object sender, EventArgs e)
        {
            GetMapSettings();
            thisInst.mapList.AddMap(mapName, minUTMX, minUTMY, gridReso, numX, numY, whatToMap, powerCurve, thisInst, false, new Wake_Model(), metsUsed, models, useTimeSeries);
            Close();
        }
    }
}
