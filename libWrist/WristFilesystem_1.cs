using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace libWrist
{
    public class WristFilesystem
    {
        public enum Sides
        {
            LEFT,
            RIGHT,
        }

        public enum Databases
        {
            DATA,
            DATABASE,
        }

        private struct SeriesInfo
        {
            public string series;
            public int seriesNum;
            public string motionFile;
        }

        public enum BIndex
        {
            RAD = 0,
            ULN = 1,
            SCA = 2,
            LUN = 3,
            TRQ = 4,
            PIS = 5,
            TPD = 6,
            TPM = 7,
            CAP = 8,
            HAM = 9,
            MC1 = 10,
            MC2 = 11,
            MC3 = 12,
            MC4 = 13,
            MC5 = 14
        }

        private static string[] _bnames = { "rad", "uln", "sca", "lun", "trq", "pis", "tpd", "tpm", "cap", "ham", "mc1", "mc2", "mc3", "mc4", "mc5" };
        private static string[] _longBnames = { "Radius", "Ulna", "Scaphoid", "Lunate", "Triquetrum", "Pisiform", "Trapezoid", "Trapezium", "Capitate", "Hamate", "MC1", "MC2", "MC3", "MC4", "MC5" };
        private static int[][] _boneInteraction = { 
            new int[] {1,2,3}, //Rad - sca, lun, uln
            new int[] {0,3,4}, //uln - rad, lun, trq
            new int[] {0,3,6,7,8}, //sca - rad, lun, cap, tpm, tpd
            new int[] {0,1,2,8,9,4}, //lun - rad, uln, sca, cap, ham, trq                                        
            new int[] {3,9,5}, //trq - lun, ham, pis
            new int[] {4}, //pis - trq
            new int[] {2,8,7,11,12}, //tpd - sca, cap, tpd, mc2, mc3
            new int[] {2,6,10,11}, //tpm - sca, tpm, mc1, mc2
            new int[] {2,3,9,6,11,12,13}, //cap - sca, lun, ham, tpd, mc2, mc3, mc4
            new int[] {3,4,8,12,13,14}, //ham - lun, trq, cap, mc3, mc4, mc5
            new int[] {7}, //mc1 - tpm
            new int[] {6,7,8,12}, //mc2 - tpd, tpm, cap, mc3
            new int[] {6,8,9,11,13}, //mc3 - tpd, cap, ham, mc2, mc4
            new int[] {8,9,12,14}, //mc4 - cap, ham, mc3, mc5
            new int[] {9,13} //mc5 - ham, mc4
            };

        private static int[] _carpalBoneIndexes = { 2, 3, 4, 5, 6, 7, 8, 9 };
        private static int[] _metacarpalBoneIndexes = { 10, 11, 12, 13, 14 };


        private string[] _bpaths;
        private string[] _distanceFieldPaths;

        private string _subjectPath;
        private string _subject;
        private string _ivFolderPath;
        private string _neutralSeriesNum;
        private string _neutralSeries;
        private string _side;
        private string _radius;
        private string _inertiaFile;
        private string _acsFile;
        private string _acsFile_uln;
        private Databases _db;
        private SeriesInfo[] _info;

        //this is precompiled crap
        //private string[] _series;

        public WristFilesystem(string pathRadiusIV)
        {
            _radius = pathRadiusIV;
            _bpaths = new string[_bnames.Length];
            _distanceFieldPaths = new string[_bnames.Length];
            setupPaths();
            findAllSeries();
        }

        public WristFilesystem()
        {
        }

        public void setupWrist(string pathRadiusIV)
        {
            _radius = pathRadiusIV;
            _bpaths = new string[_bnames.Length];
            _distanceFieldPaths = new string[_bnames.Length];
            setupPaths();
            findAllSeries();
        }

        #region Public Accessors

        /// <summary>
        /// The wrist's subject (ie. E02366 (data) or 12345 (database))
        /// </summary>
        public string subject
        {
            get { return _subject; }
        }

        /// <summary>
        /// Full path to the radius IV file that was originally passed in
        /// </summary>
        public string Radius
        {
            get { return _radius; }
        }

        /// <summary>
        /// Full path to the subject folder. (ie p:\data\young\E02366)
        /// </summary>
        public string subjectPath
        {
            get { return _subjectPath; }
        }

        /// <summary>
        /// Which hand the current wrist really is (R or L)
        /// </summary>
        public string side
        {
            get { return _side; }
        }

        /// <summary>
        /// The series ID for the neutral (ie. S15L for data). Note: for Database wrists always returns "Neut"
        /// </summary>
        public string neutralSeries
        {
            get { return _neutralSeries; }
        }

        /// <summary>
        /// Returns the full full path to the neutral series folder
        /// </summary>
        public string NeutralSeriesPath
        {
            get { return Path.Combine(_subjectPath, _neutralSeries); }
        }

        /// <summary>
        /// The full path to the neutral intertia file
        /// </summary>
        public string inertiaFile
        {
            get { return _inertiaFile; }
        }

        /// <summary>
        /// The full path to the neutral ACS file
        /// </summary>
        public string acsFile
        {
            get { return _acsFile; }
        }

        /// <summary>
        /// The full path to the neutral ACS file for the ulna if it exists.
        /// </summary>
        public string acsFile_uln
        {
            get { return _acsFile_uln; }
        }


        /// <summary>
        /// An array of full paths to all 15 bones for the wrist
        /// </summary>
        public string[] bpaths
        {
            get { return _bpaths; }
        }

        /// <summary>
        /// An array containingn the full path to the distance fields for each bone
        /// (ie {"p:\Data\...\E00001\S15R\DistanceFields\cap15R_mri", "p:\Data\...\E00001\S15R\DistanceFields\ham15R_mri",...})
        /// </summary>
        public string[] DistanceFieldPaths
        {
            get { return _distanceFieldPaths; }
        }

        /// <summary>
        /// An array of the series in this subject for this side (ie {"S01L", "S02L", "S03L",...})
        /// </summary>
        public string[] series
        {
            get
            {
                string[] s = new string[_info.Length];
                for (int i = 0; i < _info.Length; i++)
                    s[i] = _info[i].series;
                return s;
            }
        }

        /// <summary>
        /// An array of the full paths to all the motion files for the subject. One per non-neutral series
        /// </summary>
        public string[] motionFiles
        {
            get
            {
                string[] s = new string[_info.Length];
                for (int i = 0; i < _info.Length; i++)
                    s[i] = _info[i].motionFile;
                return s;
            }
        }

        /// <summary>
        /// The path to the SeriesNames.ini file (optional) that can give nice names for each series
        /// </summary>
        public string SeriesNamesFilename
        {
            get
            {
                return Path.Combine(_subjectPath, @"SeriesNames.ini");
            }
        }

        public static string[] ShortBoneNames
        {
            get { return _bnames; }
        }

        public static string[] LongBoneNames
        {
            get { return _longBnames; }
        }

        public static int NumBones
        {
            get { return _bnames.Length; }
        }

        public static int[][] BoneInteractionIndex
        {
            get { return _boneInteraction; }
        }

        /// <summary>
        /// Indexes for the eight carpal bones as they fall in the wrist arrays.
        /// </summary>
        public static int[] CarpalBoneIndexes
        {
            get { return _carpalBoneIndexes; }
        }

        /// <summary>
        /// Indexes for the 5 metacarpal bones as they fall in the wrist arrays.
        /// </summary>
        public static int[] MetacarpalBoneIndexes
        {
            get { return _metacarpalBoneIndexes; }
        }

        #endregion

        public static int GetBoneIndexFromShortName(string shortname)
        {
            shortname = shortname.ToLower();
            for (int i = 0; i < _bnames.Length; i++)
                if (shortname.Equals(_bnames[i]))
                    return i;

            //if we get here, we failed.... return -1 or throw exception...?
            throw new ArgumentException(String.Format("'{0}' is not a valid bone name", shortname));
        }

        public static Sides GetSideFromString(string sideString)
        {
            switch (sideString.ToLower())
            {
                case "l":
                case "left":
                    return Sides.LEFT;
                case "r":
                case "right":
                    return Sides.RIGHT;
                default:
                    throw new ArgumentException(String.Format("Invalid side: '{0}'", sideString));
            }
        }

        public static bool IsValidSideString(string side)
        {
            switch (side.ToLower())
            {
                case "l":
                case "left":
                case "r":
                case "right":
                    return true;
                default:
                    return false;
            }
        }

        #region Public Get Methods
        /// <summary>
        /// Given a series, it will try and find the index for it in the current object. (Case sensitive)
        /// </summary>
        /// <param name="series">Series name to check</param>
        /// <returns>Index to the series, 0 is neutral</returns>
        public int getSeriesIndexFromName(string series)
        {
            //check for neutral
            if (_neutralSeries.Equals(series)) return 0;

            for (int i = 0; i < _info.Length; i++)
                if (_info[i].series.Equals(series)) return i+1;

            throw new ArgumentException("Unable to locate series in list");
        }

        /// <summary>
        /// Returns the full full path to the motion file for the given position index
        /// </summary>
        /// <param name="positionIndex">Index of the position you are looking for</param>
        /// <returns>Full path to motion file</returns>
        public string getMotionFilePath(int positionIndex)
        {
            if (positionIndex >= _info.Length)
                throw new ArgumentOutOfRangeException("Position index exceeds length of array.");

            return _info[positionIndex].motionFile;
        }

        /// <summary>
        /// Returns the full full path to the series folder for the given index
        /// </summary>
        /// <param name="positionIndex">Index of the position you are looking for</param>
        /// <returns>Full apth to the series folder</returns>
        public string getSeriesPath(int positionIndex)
        {
            if (positionIndex >= _info.Length)
                throw new ArgumentOutOfRangeException("Position index exceeds length of array.");

            return Path.Combine(_subjectPath, _info[positionIndex].series);
        }

        #endregion

        private void findAllSeries()
        {
            if (_db == Databases.DATA)
                findAllSeries_Data();
            else
                findAllSeries_Database();
        }

        /// <summary>
        /// Tries to find all of the series for the current wrist, assuming a Database structure.
        /// Searches the subject folder for folders with the correct name (ie 01R or 02R)
        /// A folder is then checked for the existance of the requisite Motion file (ie 12345_Motion_01R.dat)
        /// All folders matching these conditions are added to a new array stored in _info
        /// </summary>
        private void findAllSeries_Database()
        {
            DirectoryInfo sub = new DirectoryInfo(_subjectPath);
            DirectoryInfo[] series = sub.GetDirectories("??" + _side);
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            foreach (DirectoryInfo s in series)
            {
                if (!Regex.Match(s.Name, @"(\d\d)" + _side, RegexOptions.IgnoreCase).Success)
                    continue;

                string motion = Path.Combine(s.FullName, _subject + "_Motion" + s.Name + ".dat");
                if (File.Exists(motion))
                {
                    SeriesInfo info = new SeriesInfo();
                    info.motionFile = motion;
                    info.series = s.Name;
                    info.seriesNum = Int32.Parse(s.Name.Substring(0, 2));

                    list.Add(info);
                }
            }
            _info = (SeriesInfo[])list.ToArray(typeof(SeriesInfo));
        }

        /// <summary>
        /// Tries to find all of the series for the current wrist, assuming a Database structure. 
        /// Searches the subject folder for folders with the correct name (ie S01R or S02R)
        /// A folder is then checked for the existance of the requisite Motion file (ie Motion15R01R.dat)
        /// All folders matching these conditions are added to a new array stored in _info
        /// </summary>
        private void findAllSeries_Data()
        {
            DirectoryInfo sub = new DirectoryInfo(_subjectPath);
            DirectoryInfo[] series = sub.GetDirectories("S??" + _side);
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            foreach (DirectoryInfo s in series)
            {
                if (!Regex.Match(s.Name,@"S(\d\d)"+_side,RegexOptions.IgnoreCase).Success)                
                    continue;

                string motion = Path.Combine(s.FullName, "Motion" + _neutralSeriesNum + _side + s.Name.Substring(1, 2) + _side + ".dat");
                if (File.Exists(motion))
                {
                    SeriesInfo info = new SeriesInfo();
                    info.motionFile = motion;
                    info.series = s.Name;
                    info.seriesNum = Int32.Parse(s.Name.Substring(1, 2));

                    list.Add(info);
                }
            }
            _info = (SeriesInfo[])list.ToArray(typeof(SeriesInfo));
        }

        public void setupPaths() { setupPaths(_radius); }
        private void setupPaths(string pathRadiusIV)
        {
            if (isDataStructure(pathRadiusIV))
                setupPaths_Data(pathRadiusIV);
            else if (isDatabaseStructure(pathRadiusIV))
                setupPaths_Database(pathRadiusIV);
            else
                throw new ArgumentException("Bone provided is not a radius");
        }

        private void setupPaths_Database(string pathRadiusIV)
        {
            if (!isDatabaseStructure(pathRadiusIV))
                throw new ArgumentException("Bone provided is not a radius for a database");

            string fname = Path.GetFileNameWithoutExtension(pathRadiusIV);
            string ext = Path.GetExtension(pathRadiusIV);
            _db = Databases.DATABASE;

            _ivFolderPath = Path.GetDirectoryName(pathRadiusIV);
            _neutralSeriesNum = "0";
            _neutralSeries = "Neut";
            _side = fname.Substring(10, 1).ToUpper();

            /* For the database, the filestructure is more rigid, so its easier to find 
             * everything
             */

            //first check if the IV files are in the right place
            string folderName = _side.Equals("L") ? "leftiv" : "rightiv";
            if (!Path.GetFileName(_ivFolderPath).ToLower().Equals(folderName))
            {
                throw new ArgumentException("IV Folder is of the wrong filename format");
            }

            _subjectPath = Path.GetDirectoryName(_ivFolderPath);
            _subject = Path.GetFileName(_subjectPath);
            string infoFolder = Path.Combine(_subjectPath,_side.Equals("L") ? "LeftInfo" : "RightInfo");
            _inertiaFile = Path.Combine(infoFolder,_subject + "_inertia_" + _side + ".dat");
            _acsFile = Path.Combine(infoFolder, _subject + "_RCS_" + _side + ".dat");

            //Now verify that this is the subject path
            if (!Regex.Match(Path.GetFileName(_subjectPath), @"^\d{5}$").Success)
                throw new ArgumentException("Invalid subject path: " + _subjectPath);

            //now setup bonenames & paths
            for (int i = 0; i < _bnames.Length; i++)
                _bpaths[i] = Path.Combine(_ivFolderPath, _subject + "_" + _bnames[i] + "_" + _side + ext);
        }

        private void setupPaths_Data(string pathRadiusIV)
        {
            if (!isDataStructure(pathRadiusIV))
                throw new ArgumentException("Bone provided is not a radius for a data");

            _db = Databases.DATA;

            string fname = Path.GetFileNameWithoutExtension(pathRadiusIV);
            string ext = Path.GetExtension(pathRadiusIV);

            _ivFolderPath = Path.GetDirectoryName(pathRadiusIV);
            _neutralSeriesNum = fname.Substring(3, 2);
            _side = fname.Substring(5, 1).ToUpper();

            _neutralSeries = "S" + _neutralSeriesNum + _side;


            //now setup bonenames & paths
            for (int i = 0; i < _bnames.Length; i++)
                _bpaths[i] = Path.Combine(_ivFolderPath, _bnames[i] + _neutralSeriesNum + _side + ext);

            /* Finding the subject path
             * - Try and find the directory of the neutral series and then set the 
             * subject path to its parent directory.
             */

            
            //first check if the neutral series path contains the IV files
            if (Path.GetFileName(_ivFolderPath).ToUpper().Equals(_neutralSeries))
            {
                _subjectPath = Path.GetDirectoryName(_ivFolderPath);
            }
            //else check to see if its one level above
            else if (Path.GetFileName(Path.GetDirectoryName(_ivFolderPath)).ToUpper().Equals(_neutralSeries))
            {
                _subjectPath = Path.GetDirectoryName(Path.GetDirectoryName(_ivFolderPath));
            }
            else
                throw new ArgumentException("Unable to locate subject path");

            _subject = Path.GetFileName(_subjectPath);
            _inertiaFile = Path.Combine(Path.Combine(_subjectPath,_neutralSeries),"inertia"+ _neutralSeriesNum + _side + ".dat");
            _acsFile = Path.Combine(Path.Combine(_subjectPath, _neutralSeries), "AnatCoordSys.dat");
            _acsFile_uln = Path.Combine(Path.Combine(_subjectPath, _neutralSeries), "AnatCoordSys_uln.dat");

            //setup Distance fields
            string distanceFieldFolder = Path.Combine(Path.Combine(_subjectPath,_neutralSeries),"DistanceFields");
            for (int i = 0; i < _bnames.Length; i++)
                _distanceFieldPaths[i] = Path.Combine(distanceFieldFolder, String.Format("{0}{1}{2}_mri", _bnames[i], _neutralSeriesNum, _side));

            //Now verify that this is the subject path
            if (!Regex.Match(Path.GetFileName(_subjectPath), @"E\d{5}").Success)
                throw new ArgumentException("Invalid subject path: " + _subjectPath);

        }

        #region Static Validator Functions
        /// <summary>
        /// Tests if the given string is a valid path for a Database structure wirst, tests based on a radius bone IV file
        /// </summary>
        /// <param name="radiusPath">Path to radius file to test</param>
        /// <returns></returns>
        static public bool isDatabaseStructure(string radiusPath)
        {
            string fname = Path.GetFileNameWithoutExtension(radiusPath);
            return (Regex.Match(fname, @"^\d{5}_rad_[lr]$", RegexOptions.IgnoreCase).Success);
        }

        /// <summary>
        /// Tests if the given string is a valid path for a Data structure wirst, tests based on a radius bone IV file 
        /// </summary>
        /// <param name="radiusPath">Path to radius file to test</param>
        /// <returns></returns>
        static public bool isDataStructure(string radiusPath)
        {
            string fname = Path.GetFileNameWithoutExtension(radiusPath);
            return (Regex.Match(fname, @"^rad\d{2}[lr]$", RegexOptions.IgnoreCase).Success);;
        }

        /// <summary>
        /// Tests if the given file is a radius bone IV file in either the Data or Database naming format. 
        /// Takes in an array of filenames to allow more diverse checking.
        /// Note: does not check for a radius in the list, rather it checks that there is only a single file, and that it is a radius.
        /// </summary>
        /// <param name="filenames">List of filenames to check (for a radius, can only be length==1</param>
        /// <returns></returns>
        static public bool isRadius(string[] filenames)
        {
            if (filenames.Length != 1) return false;
            return isRadius(filenames[0]);
        }

        /// <summary>
        /// Tests if the given file is a radius bone IV file in either the Data or Database naming format
        /// </summary>
        /// <param name="path">Path to radius file to test</param>
        /// <returns></returns>
        static public bool isRadius(string path)
        {
            return (isDataStructure(path) || isDatabaseStructure(path));
        }

        /// <summary>
        /// Tries to locate a left radius for the given subject in either a data or Database format. Returns the empty string on failure
        /// </summary>
        /// <param name="subjectPath">Subject path to look at</param>
        /// <returns>The full path to the radius, empty string if none can be found</returns>
        static public string findLeftRadius(string subjectPath)
        {
            return findRadius(subjectPath, "Left");
        }

        /// <summary>
        /// Tries to locate a right radius for the given subject in either a data or Database format. Returns the empty string on failure
        /// </summary>
        /// <param name="subjectPath">Subject path to look at</param>
        /// <returns>The full path to the radius, empty string if none can be found</returns>
        static public string findRightRadius(string subjectPath)
        {
            return findRadius(subjectPath, "Right");
        }

        /// <summary>
        /// Tries to locate the radius for the given subject in either a data or Database format. Returns the empty string on failure
        /// </summary>
        /// <param name="subjectPath">Subject path to look at</param>
        /// <param name="side">Whether to look for a right or left radius</param>
        /// <returns>The full path to the radius, empty string if none can be found</returns>
        static public string findRadius(string subjectPath, Sides side)
        {
            switch (side)
            {
                case Sides.LEFT:
                    return findRadius(subjectPath, "Left");
                case Sides.RIGHT:
                    return findRadius(subjectPath, "Right");
                default:
                    throw new WristException("Unknown side"); //unreachable, removes compiler warning
            }
        }

        /// <summary>
        /// Given the series, (ie "S15R"), return if the series is for a left or right wrist
        /// </summary>
        /// <param name="series"></param>
        /// <exception cref="WristException">Thrown if the string is not a valid series string</exception>
        /// <returns></returns>
        static public Sides getSideFromSeries(string series)
        {
            Match m = Regex.Match(series, @"^S?\d{2}([LR])$", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new WristException("Unable to determine side from series");

            switch (m.Groups[1].Value.ToUpper())
            {
                case "L":
                    return Sides.LEFT;
                case "R":
                    return Sides.RIGHT;
                default:
                    throw new WristException(String.Format("Invalid Side found ({0}).", m.Groups[1].Value));
            }
        }

        /// <summary>
        /// Tries to locate a radius for the given subject, for a given side, in either a data or Database format. Returns the empty string on failure
        /// </summary>
        /// <param name="subjectPath">Subject path to look at</param>
        /// <param name="side">The side to look for, must be either "Left" or "Right"</param>
        /// <returns>The full path to the radius, empty string if none can be found</returns>
        static private string findRadius(string subjectPath, string side)
        {
            string subject = Path.GetFileName(subjectPath);
            //check if subject is in Data format
            if (Regex.Match(subject, @"^E\d{5}$", RegexOptions.IgnoreCase).Success)
            {
                // 1) Find Neutral series folder
                string seriesPath = Path.Combine(subjectPath,"S15"+side.Substring(0,1));
                string series = "15"+side.Substring(0,1);
                if (!Directory.Exists(seriesPath)) {
                    //not a 15, shit, lets try some more stuff
                    DirectoryInfo subjectFolder = new DirectoryInfo(subjectPath);
                    DirectoryInfo[] dirs = subjectFolder.GetDirectories("S??" + side.Substring(0, 1));
                    if (dirs.Length == 0) return "";
                    seriesPath = dirs[0].FullName;
                    series = dirs[0].Name.Substring(1, 3);
                }
                // 2) Now try and find the IV folder and the radius
                string rad = Path.Combine(Path.Combine(seriesPath,"IV.Files"),"rad"+series+".iv");
                if (File.Exists(rad))
                    return rad;
                else if (File.Exists(Path.Combine(seriesPath,"rad"+series+".iv")))
                    return Path.Combine(seriesPath,"rad"+series+".iv");
                else 
                    return "";

                //DirectoryInfo ivFolder = new DirectoryInfo(Path.Combine(subjectPath, side + "IV"));
                //ivFolder.GetFiles("")
            }
            else if (Regex.Match(subject, @"^\d{5}$", RegexOptions.IgnoreCase).Success)
            {
                //in this case its in Database format, much easier
                // 1) Check that the IV folder exists
                if (!Directory.Exists(Path.Combine(subjectPath, side + "IV"))) return "";
                // 2) Check if a radius file exists
                string rad = Path.Combine(Path.Combine(subjectPath, side + "IV"),subject+"_rad_"+side.Substring(0,1)+".iv");
                if (File.Exists(rad))
                    return rad;
                else 
                    return "";
            }
            else
                throw new ArgumentException("Not a valid subject path: " + subjectPath);
        }

        #endregion

        /// <summary>
        /// Tests if the path this object was initialized with is a valid Radius IV filename in either the Data or Database format
        /// </summary>
        /// <returns></returns>
        public bool isRadius()
        {
            return isRadius(_radius);
        }
    }
}
