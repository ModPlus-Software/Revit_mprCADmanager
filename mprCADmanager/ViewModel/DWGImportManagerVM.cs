namespace mprCADmanager.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Input;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Commands;
    using Enums;
    using Model;
    using ModPlusAPI;
    using ModPlusAPI.Mvvm;
    using ModPlusAPI.Windows;
    using Revit;
    using Visibility = System.Windows.Visibility;

    // ReSharper disable once InconsistentNaming
    public class DWGImportManagerVM : VmBase
    {
        private const string LangItem = "mprCADmanager";
        private readonly UIApplication _uiApplication;
        private readonly DeleteElementEvent _deleteElementEvent;
        private readonly ChangeViewEvent _changeViewEvent;
        private readonly DeleteManyElementsEvent _deleteManyElementsEvent;
        private BelongingToViewVariant _currentBelongingToViewVariant;
        private InsertTypeVariant _currentInsertTypeVariant;
        private string _searchText = string.Empty;

        public DWGImportManagerVM(
            UIApplication uiApplication,
            List<Element> elements,
            DeleteElementEvent deleteElementEvent,
            ChangeViewEvent changeViewEvent,
            DeleteManyElementsEvent deleteManyElementsEvent)
        {
            _deleteElementEvent = deleteElementEvent;
            _changeViewEvent = changeViewEvent;
            _deleteManyElementsEvent = deleteManyElementsEvent;
            _uiApplication = uiApplication;
            DwgImportsItems = new ObservableCollection<DwgImportsItem>();
            DwgImportsItems.CollectionChanged += (sender, args) => OnPropertyChanged(nameof(SelectedItemsCount));
            FillDwgImportsItems(elements);
        }

        /// <summary>
        /// Удалить выбранные элементы
        /// </summary>
        public ICommand DeleteSelectedCommand => new RelayCommandWithoutParameter(DeleteSelectedItems);

        /// <summary>
        /// Выбрать все элементы
        /// </summary>
        public ICommand SelectAllCommand => new RelayCommandWithoutParameter(SelectAll);

        /// <summary>
        /// Коллекция обозначений импорта
        /// </summary>
        public ObservableCollection<DwgImportsItem> DwgImportsItems { get; }

        /// <summary>
        /// Количество выбранных элементов
        /// </summary>
        public int SelectedItemsCount => DwgImportsItems.Count(i => i.Visibility == Visibility.Visible && i.IsSelected);

        /// <summary>
        /// Текущий выбранный вариант сортировки по принадлежности виду
        /// </summary>
        public BelongingToViewVariant CurrentBelongingToViewVariant
        {
            get => _currentBelongingToViewVariant;
            set
            {
                if (_currentBelongingToViewVariant == value)
                    return;
                _currentBelongingToViewVariant = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        /// <summary>
        /// Текущий выбранный вариант фильтрации по типу вставки
        /// </summary>
        public InsertTypeVariant CurrentInsertTypeVariant
        {
            get => _currentInsertTypeVariant;
            set
            {
                if (_currentInsertTypeVariant == value)
                    return;
                _currentInsertTypeVariant = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        /// <summary>
        /// Строка для поиска
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;
                _searchText = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        private void FillDwgImportsItems(IEnumerable<Element> collector)
        {
            DwgImportsItems.Clear();
            foreach (var element in collector)
            {
                var dwgImportsItem = new DwgImportsItem(element, _uiApplication, this, _deleteElementEvent, _changeViewEvent);
                dwgImportsItem.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == "IsSelected")
                        OnPropertyChanged(nameof(SelectedItemsCount));
                };

                DwgImportsItems.Add(dwgImportsItem);
            }
        }

        private void DeleteSelectedItems()
        {
            try
            {
                var ids = DwgImportsItems
                    .Where(i => i.Visibility == Visibility.Visible && i.IsSelected)
                    .Select(i => i.Id).ToList();
                if (!ids.Any())
                    return;

                DWGImportManagerCommand.MainWindow.Topmost = false;
                var taskDialog = new TaskDialog(Language.GetItem(LangItem, "h1"))
                {
                    MainContent = Language.GetItem(LangItem, "msg1"),
                    CommonButtons = TaskDialogCommonButtons.None
                };
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, Language.GetItem(LangItem, "yes"));
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, Language.GetItem(LangItem, "no"));
                var result = taskDialog.Show();
                if (result != TaskDialogResult.CommandLink1)
                    return;

                _deleteManyElementsEvent.SetAction(ids, doc: _uiApplication.ActiveUIDocument.Document);
                
                FillDwgImportsItems(DWGImportManagerCommand.GetElements(_uiApplication.ActiveUIDocument.Document));
            }
            catch (Exception exception)
            {
                ExceptionBox.Show(exception);
            }
            finally
            {
                if (DWGImportManagerCommand.MainWindow != null)
                    DWGImportManagerCommand.MainWindow.Topmost = true;
            }
        }

        private void SelectAll()
        {
            try
            {
                foreach (var dwgImportsItem in DwgImportsItems.Where(d => d.Visibility == Visibility.Visible))
                {
                    dwgImportsItem.IsSelected = true;
                }
            }
            catch (Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }

        private void FilterItems()
        {
            var searchString = SearchText.Trim().ToUpper();
            foreach (var dwgImportsItem in DwgImportsItems)
            {
                if (!dwgImportsItem.Name.ToUpper().Contains(searchString) &&
                    !dwgImportsItem.OwnerViewName.ToUpper().Contains(searchString))
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                if (CurrentBelongingToViewVariant == BelongingToViewVariant.Unidentified &&
                    dwgImportsItem.Category != null)
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                if (CurrentBelongingToViewVariant == BelongingToViewVariant.ViewSpecific &&
                    !dwgImportsItem.ViewSpecific)
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                if (CurrentBelongingToViewVariant == BelongingToViewVariant.ModelImports &&
                    dwgImportsItem.ViewSpecific)
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                if (CurrentInsertTypeVariant == InsertTypeVariant.Linked &&
                    !dwgImportsItem.IsLinked)
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                if (CurrentInsertTypeVariant == InsertTypeVariant.Imported &&
                    dwgImportsItem.IsLinked)
                {
                    dwgImportsItem.Visibility = Visibility.Collapsed;
                    continue;
                }

                dwgImportsItem.Visibility = Visibility.Visible;
            }

            OnPropertyChanged(nameof(SelectedItemsCount));
        }
    }
}
