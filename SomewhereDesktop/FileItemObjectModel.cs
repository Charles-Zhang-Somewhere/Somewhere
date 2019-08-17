using Somewhere;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StringHelper;

namespace SomewhereDesktop
{
    /// <summary>
    /// A model for QueryRow.FileDetail with additional support for querying content,
    /// and support for MVVM notification
    /// </summary>
    public class FileItemObjectModel : QueryRows.FileDetail, INotifyPropertyChanged
    {
        #region Constructors
        public FileItemObjectModel() { }
        public FileItemObjectModel(QueryRows.FileDetail file)
        {
            this.ID = file.ID;
            this.EntryDate = file.EntryDate;
            this.Name = file.Name;
            this.Tags = file.Tags;
            this.Meta = file.Meta;
            this.RevisionTime = file.RevisionTime;
            this.RevisionCount = file.RevisionCount;
        }
        #endregion

        #region Additional Property
        public string DisplayName
            => string.IsNullOrEmpty(Name) ? "Knowledge" : Name;
        public IEnumerable<string> TagsList
            => Tags.SplitTags();
        #endregion

        #region Data Binding One-In-All Notification Extension
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Enable data-bound views to get notified
        /// </summary>
        /// <remarks>
        /// This function is completely irrelevant to any other user who doesn't use WPF for this type;
        /// Ideally it would be implemented as an extension method but we need the interface
        /// </remarks>
        public void BroadcastPropertyChange()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ID"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DisplayName"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Tag"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Meta"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Content"));
        }
        #endregion
    }
}
