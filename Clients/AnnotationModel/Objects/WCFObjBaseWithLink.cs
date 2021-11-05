namespace WebAnnotationModel.Objects
{
    /*
    /// <summary>
    /// This class is used for objects which link two other database objects with keys, such as structure links or location links
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class WCFObjBaseWithLink<T> : WCFObjBase<T>
        where T : DataObject, new()
    {

        /// <summary>
        /// Second ID is the ID with the lowest value
        /// </summary>
        protected abstract long FirstID { get; }

        /// <summary>
        /// Second ID is the ID with the highest value
        /// </summary>
        protected abstract long SecondID { get; }

        long? _ID = new long?();

        /// <summary>
        /// This should be Unique and it will be as long as our ID's don't pass 32-bit values
        /// </summary>
        internal virtual long ID
        {
            get
            {
                if(_ID.HasValue == false)
                {
                    long newID = (((FirstID % int.MaxValue) << 32) + (SecondID % int.MaxValue));
                    _ID = new long?(newID); 
                }

                //This will cause problems if we exceed the 32 bit limit
                return _ID.Value;
            }
        }
        
        
    }
     */
}
