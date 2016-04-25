﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MALClient.Comm;
using MALClient.Comm.Anime;
using MALClient.Items;
using MALClient.Models;
using MALClient.Pages;

namespace MALClient.ViewModels
{
    public class AnimeDetailsPageNavigationArgs
    {
        public readonly XElement AnimeElement;
        public readonly IAnimeData AnimeItem;
        public readonly int Id;
        public readonly object PrevPageSetup;
        public readonly string Title;
        public bool AnimeMode = true;
        public bool RegisterBackNav = true;
        public PageIndex Source;
        public int SourceTabIndex;

        public AnimeDetailsPageNavigationArgs(int id, string title, XElement element, IAnimeData animeReference,
            object args = null)
        {
            Id = id;
            Title = title;
            AnimeElement = element;
            PrevPageSetup = args;
            AnimeItem = animeReference;
        }
    }

    public interface IDetailsViewInteraction
    {
        Flyout GetWatchedEpsFlyout();
    }

    public class AnimeDetailsPageViewModel : ViewModelBase
    {
        public bool _initialized;
        //loaded fields
        private bool _loadedDetails;
        private bool _loadedRecomm;
        private bool _loadedRelated;
        private bool _loadedReviews;
        //crucial fields
        private string _imgUrl;
        private bool _animeMode;
        private IAnimeData _animeItemReference; //our connection with everything
        private int _id;
        public string Title { get; set; }
        public IDetailsViewInteraction View { private get; set; } //used to hide flyout
        //additional fields
        private int _allEpisodes;
        private float _globalScore;
        private string _alternateImgUrl;
        private List<string> _synonyms = new List<string>(); //used to increase ann's search reliability
        private string _sourceLink; //used to navigate to ann or hummingbird
        private string Type { get; set; }
        private string Status { get; set; }
        public int AllVolumes => _animeItemReference?.AllVolumes ?? 0;
        //Dates when show starts or ends airing
        private string StartDate { get; set; }
        private string EndDate { get; set; }
        //Dates set by the user
        public string MyStartDate => (_animeItemReference?.StartDate ?? "0000-00-00") == "0000-00-00" ? "Not set" : _animeItemReference?.StartDate;
        public string MyEndDate => (_animeItemReference?.EndDate ?? "0000-00-00") == "0000-00-00" ? "Not set" : _animeItemReference?.EndDate;
        public ObservableCollection<AnimeReviewData> Reviews { get; } = new ObservableCollection<AnimeReviewData>();

        public ObservableCollection<DirectRecommendationData> Recommendations { get; } =
            new ObservableCollection<DirectRecommendationData>();

        public ObservableCollection<RelatedAnimeData> RelatedAnime { get; } =
            new ObservableCollection<RelatedAnimeData>();

        public ObservableCollection<Tuple<string, string>> LeftDetailsRow { get; set; } =
            new ObservableCollection<Tuple<string, string>>();

        public ObservableCollection<Tuple<string, string>> RightDetailsRow { get; set; } =
            new ObservableCollection<Tuple<string, string>>();

        public ObservableCollection<string> LeftGenres { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> RightGenres { get; } = new ObservableCollection<string>();

        public ObservableCollection<Tuple<string, string>> Episodes { get; } =
            new ObservableCollection<Tuple<string, string>>();

        public ObservableCollection<string> OPs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EDs { get; } = new ObservableCollection<string>();

        private string SourceLink { get; set; }
        public int Id { get; set; }

        private int AllEpisodes
        {
            get { return _animeItemReference?.AllEpisodes ?? _allEpisodes; }
            set { _allEpisodes = value; }
        }

        private AnimeDetailsPageNavigationArgs _prevArgs;

        public DirectRecommendationData CurrentRecommendationsSelectedItem { get; set; }

        public async void Init(AnimeDetailsPageNavigationArgs param)
        {
            
            _initialized = false;
            LoadingGlobal = Visibility.Visible;
            await Task.Delay(5);
            _animeMode = param.AnimeMode;
            StartDateValid = false;
            EndDateValid = false;
            _alternateImgUrl = null;
            PivotItemDetailsVisibility = _animeMode ? Visibility.Visible : Visibility.Collapsed;
            Id = param.Id;
            Title = param.Title;
            _animeItemReference = param.AnimeItem;
            _loadedDetails = _loadedReviews = _loadedRecomm = _loadedRelated = false;
            if (_animeMode)
            {
                Status1Label = "Watching";
                Status5Label = "Plan to watch";
                WatchedEpsLabel = "My watched\nepisodes :";
                UpdateEpsUpperLabel = "Watched eps :";
            }
            else
            {
                Status1Label = "Reading";
                Status5Label = "Plan to read";
                WatchedEpsLabel = "My read\nchapters :";
                UpdateEpsUpperLabel = "Read chapters : ";
            }

            if (_animeItemReference == null || _animeItemReference is AnimeSearchItem ||
                (_animeItemReference is AnimeItemViewModel && !(_animeItemReference as AnimeItemViewModel).Auth))
                //if we are from search or from unauthenticated item let's look for proper abstraction
            {
                var possibleRef =
                    await ViewModelLocator.AnimeList.TryRetrieveAuthenticatedAnimeItem(param.Id, _animeMode);
                if (possibleRef == null) // else we don't have this item
                {
                    //we may only prepare for its creation
                    AddAnimeVisibility = true;
                    MyDetailsVisibility = false;
                }
                else
                    _animeItemReference = possibleRef;
            } // else we already have it

            if (_animeItemReference is AnimeItemViewModel && (_animeItemReference as AnimeItemViewModel).Auth)
            {
                //we have item on the list , so there's valid data here
                MyDetailsVisibility = true;
                AddAnimeVisibility = false;
                try
                {
                    _startDateTimeOffset = DateTimeOffset.Parse(_animeItemReference.StartDate);
                    StartDateValid = true;
                }
                catch (Exception)
                {
                    _startDateTimeOffset = DateTimeOffset.Now;
                    StartDateValid = false;
                }
                try
                {
                    _endDateTimeOffset = DateTimeOffset.Parse(_animeItemReference.EndDate);
                    EndDateValid = true;
                }
                catch (Exception)
                {
                    _endDateTimeOffset = DateTimeOffset.Now;
                    EndDateValid = false;
                }
                //Launch UI updates without triggering inner update logic -> nothng to update
                UpdateAnimeReferenceUiBindings(Id);
            }

            switch (param.Source)
            {
                case PageIndex.PageSearch:
                case PageIndex.PageMangaSearch:
                    ExtractData(param.AnimeElement);
                    if (_prevArgs != null)
                        NavMgr.RegisterBackNav(_prevArgs);
                    break;
                case PageIndex.PageAnimeList:
                case PageIndex.PageMangaList:
                case PageIndex.PageProfile:
                    await FetchData(param.Id.ToString(), param.Title);
                    if(_prevArgs != null)
                        NavMgr.RegisterBackNav(_prevArgs);
                    break;
                case PageIndex.PageAnimeDetails:
                    await FetchData(param.Id.ToString(), param.Title);
                    if (param.RegisterBackNav) //we are already going back
                        NavMgr.RegisterBackNav(param.PrevPageSetup);
                    break;
                case PageIndex.PageRecomendations:
                    ExtractData(param.AnimeElement);
                    if (_prevArgs != null)
                        NavMgr.RegisterBackNav(_prevArgs);
                    break;
            }
            _prevArgs = param;
            _prevArgs.RegisterBackNav = false;
            _prevArgs.Source = PageIndex.PageAnimeDetails;
            _initialized = true;
            DetailsPivotSelectedIndex = param.SourceTabIndex;
            //param.SourceTab == DetailsPageTabs.General  ? 0 : _animeMode ? (int)param.SourceTab : (int)param.SourceTab - 1;
        }

        private async void OpenMalPage()
        {
            await Launcher.LaunchUriAsync(new Uri($"http://myanimelist.net/{(_animeMode ? "anime" : "manga")}/{Id}"));
        }

        private async void OpenAnnPage()
        {
            await Launcher.LaunchUriAsync(new Uri(SourceLink));
        }

        private async void NavigateDetails(IDetailsPageArgs args)
        {
            await ViewModelLocator.Main
                .Navigate(PageIndex.PageAnimeDetails,
                    new AnimeDetailsPageNavigationArgs(args.Id, args.Title, null, null,
                        new AnimeDetailsPageNavigationArgs(Id, Title, null, _animeItemReference)
                        {
                            Source = PageIndex.PageAnimeDetails,
                            RegisterBackNav = false,
                            AnimeMode = _animeMode,
                            SourceTabIndex = DetailsPivotSelectedIndex
                        })
                    {Source = PageIndex.PageAnimeDetails, AnimeMode = args.Type == RelatedItemType.Anime});
        }

        #region Properties

        public string MyEpisodesBind => $"{MyEpisodes}/{(AllEpisodes == 0 ? "?" : AllEpisodes.ToString())}";

        private int MyEpisodes
        {
            get { return _animeItemReference?.MyEpisodes ?? 0; }
            set
            {
                _animeItemReference.MyEpisodes = value;
                RaisePropertyChanged(() => MyEpisodesBind);
            }
        }

        public string MyStatusBind => Utils.StatusToString(MyStatus, !_animeMode);

        private int MyStatus
        {
            get { return _animeItemReference?.MyStatus ?? (int) AnimeStatus.AllOrAiring; }
            set
            {
                _animeItemReference.MyStatus = value;
                RaisePropertyChanged(() => MyStatusBind);
            }
        }

        public string MyScoreBind => MyScore == 0 ? "Unranked" : $"{MyScore}/10";

        private int MyScore
        {
            get { return _animeItemReference?.MyScore ?? 0; }
            set
            {
                _animeItemReference.MyScore = value;
                RaisePropertyChanged(() => MyScoreBind);
            }
        }

        public string MyVolumesBind => MyVolumes.ToString();

        public int MyVolumes
        {
            get { return _animeItemReference?.MyVolumes ?? 0; }
            set { _animeItemReference.MyVolumes = value; }
        }

        private string _status1Label = "Watching";

        public string Status1Label
        {
            get { return _status1Label; }
            set
            {
                _status1Label = value;
                RaisePropertyChanged(() => Status1Label);
            }
        }

        private string _status5Label = "Plan to watch";

        public string Status5Label
        {
            get { return _status5Label; }
            set
            {
                _status5Label = value;
                RaisePropertyChanged(() => Status5Label);
            }
        }

        private string _watchedEpsLabel = "My watched\nepisodes :";

        public string WatchedEpsLabel
        {
            get { return _watchedEpsLabel; }
            set
            {
                _watchedEpsLabel = value;
                RaisePropertyChanged(() => WatchedEpsLabel);
            }
        }

        private string _updateEpsUpperLabel = "Watched eps :";

        public string UpdateEpsUpperLabel
        {
            get { return _updateEpsUpperLabel; }
            set
            {
                _updateEpsUpperLabel = value;
                RaisePropertyChanged(() => UpdateEpsUpperLabel);
            }
        }

        private bool _loadingUpdate;

        public bool LoadingUpdate
        {
            get { return _loadingUpdate; }
            set
            {
                _loadingUpdate = value;
                RaisePropertyChanged(() => LoadingUpdate);
            }
        }

        private string _synopsis;

        public string Synopsis
        {
            get { return _synopsis; }
            set
            {
                _synopsis = value;
                RaisePropertyChanged(() => Synopsis);
            }
        }

        private float GlobalScore
        {
            get { return _globalScore; }
            set
            {
                if (_animeItemReference != null)
                    _animeItemReference.GlobalScore = value;
                _globalScore = value;
            }
        }

        private Visibility _loadingGlobal;

        public Visibility LoadingGlobal
        {
            get { return _loadingGlobal; }
            set
            {
                _loadingGlobal = value;
                RaisePropertyChanged(() => LoadingGlobal);
            }
        }

        private Visibility _loadingDetails;

        public Visibility LoadingDetails
        {
            get { return _loadingDetails; }
            set
            {
                _loadingDetails = value;
                RaisePropertyChanged(() => LoadingDetails);
            }
        }

        private Visibility _loadingReviews;

        public Visibility LoadingReviews
        {
            get { return _loadingReviews; }
            set
            {
                _loadingReviews = value;
                RaisePropertyChanged(() => LoadingReviews);
            }
        }

        private Visibility _loadingRelated;

        public Visibility LoadingRelated
        {
            get { return _loadingRelated; }
            set
            {
                _loadingRelated = value;
                RaisePropertyChanged(() => LoadingRelated);
            }
        }

        private Visibility _loadingRecommendations;

        public Visibility LoadingRecommendations
        {
            get { return _loadingRecommendations; }
            set
            {
                _loadingRecommendations = value;
                RaisePropertyChanged(() => LoadingRecommendations);
            }
        }

        private Visibility _detailedDataVisibility;

        public Visibility DetailedDataVisibility
        {
            get { return _detailedDataVisibility; }
            set
            {
                _detailedDataVisibility = value;
                RaisePropertyChanged(() => DetailedDataVisibility);
            }
        }

        private DateTimeOffset _startDateTimeOffset;//= DateTimeOffset.Parse("2015-09-10");
        public bool StartDateValid;
        public DateTimeOffset StartDateTimeOffset
        {
            get { return _startDateTimeOffset; }
            set
            {
                _startDateTimeOffset = value;
                _animeItemReference.StartDate = value.ToString("yyyy-MM-dd");
                StartDateValid = true;
                LaunchUpdate();
                RaisePropertyChanged(() => StartDateTimeOffset);
                RaisePropertyChanged(() => MyStartDate);
            }
        }

        private DateTimeOffset _endDateTimeOffset;
        public bool EndDateValid;
        public DateTimeOffset EndDateTimeOffset
        {
            get { return _endDateTimeOffset; }
            set
            {
                _endDateTimeOffset = value;
                _animeItemReference.EndDate = value.ToString("yyyy-MM-dd");
                EndDateValid = true;
                LaunchUpdate();
                RaisePropertyChanged(() => EndDateTimeOffset);
                RaisePropertyChanged(() => MyEndDate);
            }
        }

        private string _watchedEpsInput;

        public string WatchedEpsInput
        {
            get { return _watchedEpsInput; }
            set
            {
                _watchedEpsInput = value;
                RaisePropertyChanged(() => WatchedEpsInput);
            }
        }

        private bool _watchedEpsInputNoticeVisibility;

        public bool WatchedEpsInputNoticeVisibility
        {
            get { return _watchedEpsInputNoticeVisibility; }
            set
            {
                _watchedEpsInputNoticeVisibility = value;
                RaisePropertyChanged(() => WatchedEpsInputNoticeVisibility);
            }
        }

        private bool _myDetailsVisibility;

        public bool MyDetailsVisibility
        {
            get { return _myDetailsVisibility; }
            set
            {
                _myDetailsVisibility = value;
                RaisePropertyChanged(() => MyDetailsVisibility);
            }
        }

        private bool _addAnimeVisibility;

        public bool AddAnimeVisibility
        {
            get { return _addAnimeVisibility; }
            set
            {
                _addAnimeVisibility = value;
                RaisePropertyChanged(() => AddAnimeVisibility);
            }
        }

        private ICommand _saveImageCommand;
        public ICommand SaveImageCommand
        {
            get
            {
                return _saveImageCommand ??
                       (_saveImageCommand =
                           new RelayCommand<string>(
                                async opt =>
                               {
                                   if (_animeMode || (!_animeMode && opt != "hum"))
                                       Utils.DownloadCoverImage(
                                           opt == "hum" ? (_alternateImgUrl ?? (_alternateImgUrl = await LoadHummingbirdCoverImage())) : _imgUrl, Title);
                               }));
            }
        }

        private ICommand _changeStatusCommand;

        public ICommand ChangeStatusCommand
        {
            get { return _changeStatusCommand ?? (_changeStatusCommand = new RelayCommand<object>(ChangeStatus)); }
        }

        private ICommand _resetStartDateCommand;
        public ICommand ResetStartDateCommand
        {
            get
            {
                return _resetStartDateCommand ?? (_resetStartDateCommand = new RelayCommand(() =>
                {
                    StartDateValid = false;
                    _animeItemReference.StartDate = AnimeItemViewModel.InvalidStartEndDate;
                    RaisePropertyChanged(() => MyStartDate);
                    LaunchUpdate();
                }));
            }
        }
        private ICommand _resetEndDateCommand;
        public ICommand ResetEndDateCommand
        {
            get
            {
                return _resetEndDateCommand ?? (_resetEndDateCommand = new RelayCommand(() =>
                {
                    EndDateValid = false;
                    _animeItemReference.EndDate = AnimeItemViewModel.InvalidStartEndDate;
                    RaisePropertyChanged(() => MyEndDate);
                    LaunchUpdate();
                }));
            }
        }

        private ICommand _navigateDetailsCommand;

        public ICommand NavigateDetailsCommand
        {
            get
            {
                return _navigateDetailsCommand ?? (_navigateDetailsCommand =
                    new RelayCommand<IDetailsPageArgs>(args => NavigateDetails(args)));
            }
        }

        private ICommand _changeScoreCommand;

        public ICommand ChangeScoreCommand
        {
            get { return _changeScoreCommand ?? (_changeScoreCommand = new RelayCommand<object>(ChangeScore)); }
        }

        private ICommand _changeWatchedCommand;

        public ICommand ChangeWatchedCommand
        {
            get { return _changeWatchedCommand ?? (_changeWatchedCommand = new RelayCommand(ChangeWatchedEps)); }
        }

        private ICommand _addAnimeCommand;

        public ICommand AddAnimeCommand
        {
            get { return _addAnimeCommand ?? (_addAnimeCommand = new RelayCommand(AddAnime)); }
        }

        private ICommand _removeAnimeCommand;

        public ICommand RemoveAnimeCommand
        {
            get { return _removeAnimeCommand ?? (_removeAnimeCommand = new RelayCommand(RemoveAnime)); }
        }

        private ICommand _openInMalCommand;

        public ICommand OpenInMalCommand
        {
            get { return _openInMalCommand ?? (_openInMalCommand = new RelayCommand(OpenMalPage)); }
        }

        private ICommand _openInAnnCommand;

        public ICommand OpenInAnnCommand
        {
            get { return _openInAnnCommand ?? (_openInAnnCommand = new RelayCommand(OpenAnnPage)); }
        }

        private ICommand _copyToClipboardCommand;

        public object CopyToClipboardCommand
        {
            get
            {
                return _copyToClipboardCommand ?? (_copyToClipboardCommand = new RelayCommand(() =>
                {
                    var dp = new DataPackage();
                    dp.SetText($"http://www.myanimelist.net/{(_animeMode ? "anime" : "manga")}/{Id}");
                    Clipboard.SetContent(dp);
                    Utils.GiveStatusBarFeedback("Copied to clipboard...");
                }));
            }
        }

        private BitmapImage _detailImage;

        public BitmapImage DetailImage
        {
            get { return _detailImage; }
            set
            {
                _detailImage = value;
                RaisePropertyChanged(() => DetailImage);
            }
        }

        private Visibility _noEpisodesDataVisibility;

        public Visibility NoEpisodesDataVisibility
        {
            get { return _noEpisodesDataVisibility; }
            set
            {
                _noEpisodesDataVisibility = value;
                RaisePropertyChanged(() => NoEpisodesDataVisibility);
            }
        }

        private Visibility _noEDsDataVisibility;

        public Visibility NoEDsDataVisibility
        {
            get { return _noEDsDataVisibility; }
            set
            {
                _noEDsDataVisibility = value;
                RaisePropertyChanged(() => NoEDsDataVisibility);
            }
        }

        private Visibility _noOPsDataVisibility;

        public Visibility NoOPsDataVisibility
        {
            get { return _noOPsDataVisibility; }
            set
            {
                _noOPsDataVisibility = value;
                RaisePropertyChanged(() => NoOPsDataVisibility);
            }
        }

        private Visibility _noGenresDataVisibility;

        public Visibility NoGenresDataVisibility
        {
            get { return _noGenresDataVisibility; }
            set
            {
                _noGenresDataVisibility = value;
                RaisePropertyChanged(() => NoGenresDataVisibility);
            }
        }

        private Visibility _annSourceButtonVisibility;

        public Visibility AnnSourceButtonVisibility
        {
            get { return _annSourceButtonVisibility; }
            set
            {
                _annSourceButtonVisibility = value;
                RaisePropertyChanged(() => AnnSourceButtonVisibility);
            }
        }

        private Visibility _pivotItemDetailsVisibility = Visibility.Visible;

        public Visibility PivotItemDetailsVisibility
        {
            get { return _pivotItemDetailsVisibility; }
            set
            {
                _pivotItemDetailsVisibility = value;
                RaisePropertyChanged(() => PivotItemDetailsVisibility);
            }
        }

        private int _detailsPivotSelectedIndex;

        public int DetailsPivotSelectedIndex
        {
            get { return _detailsPivotSelectedIndex; }
            set
            {
                _detailsPivotSelectedIndex = value;
                RaisePropertyChanged(() => DetailsPivotSelectedIndex);
            }
        }

        private Visibility _noReviewsDataNoticeVisibility = Visibility.Collapsed;

        public Visibility NoReviewsDataNoticeVisibility
        {
            get { return _noReviewsDataNoticeVisibility; }
            set
            {
                _noReviewsDataNoticeVisibility = value;
                RaisePropertyChanged(() => NoReviewsDataNoticeVisibility);
            }
        }

        private Visibility _noRecommDataNoticeVisibility = Visibility.Collapsed;

        public Visibility NoRecommDataNoticeVisibility
        {
            get { return _noRecommDataNoticeVisibility; }
            set
            {
                _noRecommDataNoticeVisibility = value;
                RaisePropertyChanged(() => NoRecommDataNoticeVisibility);
            }
        }

        private Visibility _noRelatedDataNoticeVisibility = Visibility.Collapsed;

        public Visibility NoRelatedDataNoticeVisibility
        {
            get { return _noRelatedDataNoticeVisibility; }
            set
            {
                _noRelatedDataNoticeVisibility = value;
                RaisePropertyChanged(() => NoRelatedDataNoticeVisibility);
            }
        }

        private Visibility _detailsOpsVisibility = Visibility.Collapsed;

        public Visibility DetailsOpsVisibility
        {
            get { return _detailsOpsVisibility; }
            set
            {
                _detailsOpsVisibility = value;
                RaisePropertyChanged(() => DetailsOpsVisibility);
            }
        }

        private Visibility _detailsEdsVisibility = Visibility.Collapsed;

        public Visibility DetailsEdsVisibility
        {
            get { return _detailsEdsVisibility; }
            set
            {
                _detailsEdsVisibility = value;
                RaisePropertyChanged(() => DetailsEdsVisibility);
            }
        }

        private string _detailsSource;

        public string DetailsSource
        {
            get { return _detailsSource; }
            set
            {
                _detailsSource = value;
                RaisePropertyChanged(() => DetailsSource);
            }
        }

        private bool _addAnimeBtnEnableState = true;

        public bool AddAnimeBtnEnableState
        {
            get { return _addAnimeBtnEnableState; }
            set
            {
                _addAnimeBtnEnableState = value;
                RaisePropertyChanged(() => AddAnimeBtnEnableState);
            }
        }

        private bool _removeAnimeBtnEnableState = true;

        public bool RemoveAnimeBtnEnableState
        {
            get { return _removeAnimeBtnEnableState; }
            set
            {
                _removeAnimeBtnEnableState = value;
                RaisePropertyChanged(() => RemoveAnimeBtnEnableState);
            }
        }

        #endregion

        #region ChangeStuff


        #region IncrementDecrementRelay

        public bool IsIncrementButtonEnabled
            => (_animeItemReference as AnimeItemViewModel)?.IncrementEpsVisibility == Visibility.Visible;

        public bool IsDecrementButtonEnabled
            => (_animeItemReference as AnimeItemViewModel)?.DecrementEpsVisibility == Visibility.Visible;

        public ICommand IncrementEpsCommand => (_animeItemReference as AnimeItemViewModel)?.IncrementWatchedCommand;
        public ICommand DecrementEpsCommand => (_animeItemReference as AnimeItemViewModel)?.DecrementWatchedCommand;

        #endregion

        private Query GetAppropriateUpdateQuery()
        {
            if (_animeMode)
                return new AnimeUpdateQuery(Id, MyEpisodes, MyStatus, MyScore,
                    (StartDateValid ? _animeItemReference.StartDate : "0000-00-00"), //if date was untouched return "no date" value
                    (EndDateValid ? _animeItemReference.EndDate : "0000-00-00"));
            return new MangaUpdateQuery(Id, MyEpisodes, MyStatus, MyScore, MyVolumes,
                (StartDateValid ? _animeItemReference.StartDate : "0000-00-00"),
                (EndDateValid ? _animeItemReference.EndDate : "0000-00-00"));
        }

        private async void LaunchUpdate()
        {
            LoadingUpdate = true;
            await GetAppropriateUpdateQuery().GetRequestResponse();
            LoadingUpdate = false;
        }

        private async void ChangeStatus(object status)
        {
            LoadingUpdate = true;
            var prevStatus = MyStatus;
            MyStatus = Utils.StatusToInt(status as string);

            if (Settings.SetStartDateOnWatching && (string) status == "Watching" &&
                (Settings.OverrideValidStartEndDate || !StartDateValid))
            {
                _startDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.StartDate = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                StartDateValid = true;
                RaisePropertyChanged(() => StartDateTimeOffset);
                RaisePropertyChanged(() => MyStartDate);
            }
            else if (Settings.SetEndDateOnDropped && (string) status == "Dropped" &&
                     (Settings.OverrideValidStartEndDate || !EndDateValid))
            {
                _endDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.EndDate = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                EndDateValid = true;
                RaisePropertyChanged(() => EndDateTimeOffset);
                RaisePropertyChanged(() => MyEndDate);
            }
            else if (Settings.SetEndDateOnCompleted && (string) status == "Completed" &&
                     (Settings.OverrideValidStartEndDate || !EndDateValid))
            {
                _endDateTimeOffset = DateTimeOffset.Now;
                _animeItemReference.EndDate = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                EndDateValid = true;
                RaisePropertyChanged(() => EndDateTimeOffset);
                RaisePropertyChanged(() => MyEndDate);
            }

            var response = await GetAppropriateUpdateQuery().GetRequestResponse();
            if (response != "Updated")
                MyStatus = prevStatus;

            if (_animeItemReference is AnimeItemViewModel)
                if (MyStatus == (int) AnimeStatus.Completed && MyEpisodes != AllEpisodes && AllEpisodes != 0)
                {
                    await ((AnimeItemViewModel) _animeItemReference).PromptForWatchedEpsChange(AllEpisodes);
                    RaisePropertyChanged(() => MyEpisodesBind);
                }


            LoadingUpdate = false;
        }

        private async void ChangeScore(object score)
        {
            LoadingUpdate = true;
            var prevScore = MyScore;
            MyScore = Convert.ToInt32(score as string);
            var response = await GetAppropriateUpdateQuery().GetRequestResponse();
            if (response != "Updated")
                MyScore = prevScore;
            LoadingUpdate = false;
        }

        public async void ChangeWatchedEps()
        {
            LoadingUpdate = true;
            int eps, prevEps = MyEpisodes;
            if (!int.TryParse(WatchedEpsInput, out eps))
            {
                WatchedEpsInputNoticeVisibility = true;
                LoadingUpdate = false;
                return;
            }
            if (eps >= 0 && (AllEpisodes == 0 || eps <= AllEpisodes))
            {
                View.GetWatchedEpsFlyout().Hide();
                WatchedEpsInputNoticeVisibility = false;
                var prevWatched = MyEpisodes;
                MyEpisodes = eps;
                var response = await GetAppropriateUpdateQuery().GetRequestResponse();
                if (response != "Updated")
                    MyEpisodes = prevWatched;

                if (_animeItemReference is AnimeItemViewModel)
                    if (prevEps == 0 && AllEpisodes > 1 && MyEpisodes != AllEpisodes &&
                        (MyStatus == (int) AnimeStatus.PlanToWatch || MyStatus == (int) AnimeStatus.Dropped ||
                         MyStatus == (int) AnimeStatus.OnHold))
                    {
                        await
                            ((AnimeItemViewModel) _animeItemReference).PromptForStatusChange((int) AnimeStatus.Watching);
                        RaisePropertyChanged(() => MyStatusBind);
                    }
                    else if (MyEpisodes == AllEpisodes && AllEpisodes != 0)
                    {
                        await
                            ((AnimeItemViewModel) _animeItemReference).PromptForStatusChange((int) AnimeStatus.Completed);
                        RaisePropertyChanged(() => MyStatusBind);
                    }
                WatchedEpsInput = "";
            }
            else
            {
                WatchedEpsInputNoticeVisibility = true;
            }
            LoadingUpdate = false;
        }

        #endregion

        #region Add/Remove

        private async void AddAnime()
        {
            LoadingUpdate = true;
            AddAnimeBtnEnableState = false;
            var response = _animeMode
                ? await new AnimeAddQuery(Id.ToString()).GetRequestResponse()
                : await new MangaAddQuery(Id.ToString()).GetRequestResponse();
            LoadingUpdate = false;
            AddAnimeBtnEnableState = true;
            if (!response.Contains("Created") && _animeMode)
                return;
            AddAnimeVisibility = false;
            AnimeType typeA;
            MangaType typeM;
            int type = 0;
            try
            {
                if (_animeMode)
                {
                    Enum.TryParse(Type, out typeA);
                    type = (int)typeA;
                }
                else
                {
                    Enum.TryParse(Type.Replace("-", ""), out typeM);
                    type = (int)typeM;
                }
            }
            catch (Exception)
            {
               //who knows what MAL has thrown at us...
            }


            string startDate = "0000-00-00";
            if (Settings.SetStartDateOnListAdd)
            {
                startDate = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                _startDateTimeOffset = DateTimeOffset.Now; //update without mal-update
                RaisePropertyChanged(() => StartDateTimeOffset);
            }
            var animeItem = _animeMode
                            ? new AnimeItemAbstraction(true, Title, _imgUrl, type, _id, 6, 0, AllEpisodes, startDate, "0000-00-00", 0)
                            : new AnimeItemAbstraction(true, Title, _imgUrl, type, _id, 6, 0, AllEpisodes, startDate, "0000-00-00", 0, 0, AllVolumes);
            _animeItemReference = animeItem.ViewModel;

            MyScore = 0;
            MyStatus = 6;
            MyEpisodes = 0;
            GlobalScore = GlobalScore; //trigger setter of anime item
            if (Status == "Currently Airing")
                (_animeItemReference as AnimeItemViewModel).Airing = true;
            ViewModelLocator.AnimeList.AddAnimeEntry(animeItem);
            MyDetailsVisibility = true;
        }

        private async void RemoveAnime()
        {

            var uSure = false;
            var msg = new MessageDialog("Are you sure about deleting this entry from your list?");
            msg.Commands.Add(new UICommand("I'm sure", command => uSure = true));
            msg.Commands.Add(new UICommand("Cancel", command => uSure = false));
            await msg.ShowAsync();
            if (!uSure)
                return;
            LoadingUpdate = true;
            RemoveAnimeBtnEnableState = false;

            var response = _animeMode
                ? await new AnimeRemoveQuery(Id.ToString()).GetRequestResponse()
                : await new MangaRemoveQuery(Id.ToString()).GetRequestResponse();

            LoadingUpdate = false;
            RemoveAnimeBtnEnableState = true;

            if (!response.Contains("Deleted"))
                return;

            ViewModelLocator.AnimeList.RemoveAnimeEntry((_animeItemReference as AnimeItemViewModel).ParentAbstraction);

            (_animeItemReference as AnimeItemViewModel).SetAuthStatus(false, true);
            AddAnimeVisibility = true;
            MyDetailsVisibility = false;
        }

        #endregion

        #region FetchAndPopulate

        private void PopulateData()
        {
            if (_animeItemReference is AnimeItemViewModel && _animeMode)
            {
                int day = -1;
                try
                {
                    day = StartDate != AnimeItemViewModel.InvalidStartEndDate &&
                              (Status == "Currently Airing" || Status == "Not yet aired")
                        ? (int) DateTime.Parse(StartDate).DayOfWeek + 1
                        : -1;
                }
                catch (Exception)
                {
                    day = -1;
                }

                DataCache.RegisterVolatileData(Id, new VolatileDataCache
                {
                    DayOfAiring = day,
                    GlobalScore = GlobalScore,
                    AirStartDate = StartDate == AnimeItemViewModel.InvalidStartEndDate ? null : StartDate                    
                });
                ((AnimeItemViewModel) _animeItemReference).Airing = day != -1;
                DataCache.SaveVolatileData();
            }
            LeftDetailsRow.Clear();
            RightDetailsRow.Clear();

            LeftDetailsRow.Add(new Tuple<string, string>(_animeMode ? "Episodes" : "Chapters",
                AllEpisodes == 0 ? "?" : AllEpisodes.ToString()));
            LeftDetailsRow.Add(new Tuple<string, string>("Score", GlobalScore.ToString()));
            LeftDetailsRow.Add(new Tuple<string, string>("Start", StartDate == "0000-00-00" ? "?" : StartDate));
            RightDetailsRow.Add(new Tuple<string, string>("Type", Type));
            RightDetailsRow.Add(new Tuple<string, string>("Status", Status));
            RightDetailsRow.Add(new Tuple<string, string>("End", EndDate == "0000-00-00" ? "?" : EndDate));

            Synopsis = Synopsis;
            Utils.GetMainPageInstance().CurrentOffStatus = Title;

            DetailImage = new BitmapImage(new Uri(_imgUrl));
            LoadingGlobal = Visibility.Collapsed;

            if (Settings.DetailsAutoLoadDetails)
                LoadDetails();
            if (Settings.DetailsAutoLoadReviews)
                LoadReviews();
            if (Settings.DetailsAutoLoadRecomms)
                LoadRecommendations();
            if (Settings.DetailsAutoLoadRelated)
                LoadRelatedAnime();
        }

        private void ExtractData(XElement animeElement)
        {           
            GlobalScore = float.Parse(animeElement.Element("score").Value);
            Type = animeElement.Element("type").Value;
            Status = animeElement.Element("status").Value;
            Synopsis = Utils.DecodeXmlSynopsisDetail(animeElement.Element("synopsis").Value);
            StartDate = animeElement.Element("start_date").Value;
            EndDate = animeElement.Element("end_date").Value;
            _imgUrl = animeElement.Element("image").Value;
            _synonyms = animeElement.Element("synonyms").Value.Split(',').ToList();
            _synonyms.Add(animeElement.Element("english").Value);
            _synonyms.Add(Title);

            _synonyms = _synonyms.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            for (var i = 0; i < _synonyms.Count; i++)
                _synonyms[i] = Regex.Replace(_synonyms[i], @" ?\(.*?\)", string.Empty);
            //removes string from brackets (sthsth) lol ->  lol
            if (_animeItemReference == null)
                AllEpisodes = Convert.ToInt32(animeElement.Element(_animeMode ? "episodes" : "chapters").Value);
            PopulateData();
        }

        private async Task FetchData(string id, string title, bool force = false)
        {
            LoadingGlobal = Visibility.Visible;
            var elem = force ? null : await DataCache.RetrieveAnimeSearchResultsData(Id, _animeMode);
            try
            {
                if (elem == null)
                {
                    var data = "";
                    await Task.Run(async () => data = _animeMode
                        ? await new AnimeSearchQuery(Utils.CleanAnimeTitle(title)).GetRequestResponse(false)
                        : await new MangaSearchQuery(Utils.CleanAnimeTitle(title)).GetRequestResponse(false));
                    data = WebUtility.HtmlDecode(data);
                    data = data.Replace("&mdash", "").Replace("&rsquo", "").Replace("&", "");

                    var parsedData = XDocument.Parse(data);
                    
                    var elements = parsedData.Element(_animeMode ? "anime" : "manga").Elements("entry");
                    elem = elements.First(element => element.Element("id").Value == id);
                    DataCache.SaveAnimeSearchResultsData(Id, elem, _animeMode);
                }
                ExtractData(elem);
            }
            catch (Exception)
            {
                LoadingGlobal = Visibility.Collapsed;
                // no internet?              
            }

        }

        public async void RefreshData()
        {
            await FetchData(Id.ToString(), Title, true);
            if (_loadedDetails)
                LoadDetails(true);
            if (_loadedReviews)
                LoadReviews(true);
            if (_loadedRecomm)
                LoadRecommendations(true);
        }

        public async void LoadDetails(bool force = false)
        {
            if (_loadedDetails && !force && _initialized)
                return;
            _loadedDetails = true;
            LoadingDetails = Visibility.Visible;
            LeftGenres.Clear();
            RightGenres.Clear();
            Episodes.Clear();
            OPs.Clear();
            EDs.Clear();
            var currSource = DataSource.Hummingbird;
            try
            {
                AnimeDetailsData data;
                switch (Settings.PrefferedDataSource)
                {
                    case DataSource.Ann:
                        data =
                            await
                                new AnimeDetailsAnnQuery(
                                    _synonyms.Count == 1 ? Title : string.Join("&title=~", _synonyms), Id, Title)
                                    .GetGeneralDetailsData(force);
                        break;
                    case DataSource.Hummingbird:
                        data = await new AnimeDetailsHummingbirdQuery(Id).GetAnimeDetails(force);
                        break;
                    case DataSource.AnnHum:
                        data = await new AnimeDetailsAnnQuery(
                            _synonyms.Count == 1 ? Title : string.Join("&title=~", _synonyms), Id, Title)
                            .GetGeneralDetailsData(force);
                        if (data == null || data.Genres.Count == 0 || data.Episodes.Count == 0)
                            data = await new AnimeDetailsHummingbirdQuery(Id).GetAnimeDetails(force);

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                SourceLink = data.Source == DataSource.Ann
                    ? SourceLink = $"http://www.animenewsnetwork.com/encyclopedia/anime.php?id={data.SourceId}"
                    : $"https://hummingbird.me/anime/{data.SourceId}";
                //Let's try to pull moar Genres data from MAL

                DetailsSource = data.Source == DataSource.Ann ? "Source : AnimeNewsNetwork" : "Source : Hummingbird";
                currSource = data.Source;
                if (data.Source == DataSource.Ann)
                {
                    VolatileDataCache genresData;
                    if (DataCache.TryRetrieveDataForId(Id, out genresData) && genresData.Genres != null)
                    {
                        foreach (var genreMal in genresData.Genres)
                        {
                            if (
                                data.Genres.All(
                                    genreAnn =>
                                        !string.Equals(genreAnn, genreMal, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                data.Genres.Add(Utils.FirstCharToUpper(genreMal));
                            }
                        }
                    }
                }
                //Now we can build elements here
                var i = 1;
                foreach (var genre in data.Genres)
                {
                    if (i%2 == 0)
                        LeftGenres.Add(Utils.FirstCharToUpper(genre));
                    else
                        RightGenres.Add(Utils.FirstCharToUpper(genre));
                    i++;
                }
                i = 1;
                foreach (var episode in data.Episodes.Take(40))
                    Episodes.Add(new Tuple<string, string>($"{i++}.", episode));
                if (data.Episodes.Count > 40)
                    Episodes.Add(new Tuple<string, string>("?.", $"{data.Episodes.Count - 40} More episodes..."));

                if (data.Source == DataSource.Ann)
                {
                    DetailsOpsVisibility = Visibility.Visible;
                    DetailsEdsVisibility = Visibility.Visible;

                    foreach (var op in data.OPs)
                        OPs.Add(op);
                    foreach (var ed in data.EDs)
                        EDs.Add(ed);
                }
                else
                {
                    DetailsOpsVisibility = Visibility.Collapsed;
                    DetailsEdsVisibility = Visibility.Collapsed;
                }


                DetailedDataVisibility = Visibility.Visible;
                AnnSourceButtonVisibility = Visibility.Visible;
            }
            catch (Exception)
            {
                if (currSource == DataSource.Ann)
                {
                    VolatileDataCache genresData;
                    // we may fail to pull genres from ann so we have this from MAL season page 
                    if (DataCache.TryRetrieveDataForId(Id, out genresData))
                    {
                        AnnSourceButtonVisibility = Visibility.Collapsed;
                        DetailedDataVisibility = Visibility.Visible;
                        var i = 1;
                        foreach (var genre in genresData.Genres ?? new List<string>())
                        {
                            if (i%2 == 0)
                                LeftGenres.Add(Utils.FirstCharToUpper(genre));
                            else
                                RightGenres.Add(Utils.FirstCharToUpper(genre));
                            i++;
                        }
                    }
                }
                else
                    DetailedDataVisibility = Visibility.Collapsed;
            }
            NoEpisodesDataVisibility = Episodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoGenresDataVisibility = LeftGenres.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoEDsDataVisibility = EDs.Count == 0 && currSource == DataSource.Ann
                ? Visibility.Visible
                : Visibility.Collapsed;
            NoOPsDataVisibility = OPs.Count == 0 && currSource == DataSource.Ann
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (Episodes.Count == 0 && LeftGenres.Count == 0 && EDs.Count == 0 && OPs.Count == 0)
                DetailedDataVisibility = Visibility.Collapsed;

            LoadingDetails = Visibility.Collapsed;
        }

        public async void LoadReviews(bool force = false)
        {
            if (_loadedReviews && !force && _initialized)
                return;
            LoadingReviews = Visibility.Visible;
            _loadedReviews = true;
            Reviews.Clear();
            var revs = new List<AnimeReviewData>();
            await Task.Run(async () => revs = await new AnimeReviewsQuery(Id, _animeMode).GetAnimeReviews(force));
            if (revs == null)
            {
                LoadingReviews = Visibility.Collapsed;
                NoReviewsDataNoticeVisibility = Visibility.Visible;
                return;                
            }
            foreach (var rev in revs)
                Reviews.Add(rev);
            NoReviewsDataNoticeVisibility = Reviews.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            LoadingReviews = Visibility.Collapsed;
        }

        public async void LoadRecommendations(bool force = false)
        {
            if (_loadedRecomm && !force && _initialized)
                return;
            LoadingRecommendations = Visibility.Visible;
            _loadedRecomm = true;
            Recommendations.Clear();
            var recomm = new List<DirectRecommendationData>();
            await
                Task.Run(
                    async () =>
                        recomm =
                            await new AnimeDirectRecommendationsQuery(Id, _animeMode).GetDirectRecommendations(force));
            if (recomm == null)
            {
                LoadingRecommendations = Visibility.Collapsed;
                NoRecommDataNoticeVisibility = Visibility.Visible;
                return;
            }
            foreach (var item in recomm)
                Recommendations.Add(item);
            NoRecommDataNoticeVisibility = Recommendations.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            LoadingRecommendations = Visibility.Collapsed;
        }

        public async void LoadRelatedAnime(bool force = false)
        {
            if (_loadedRelated && !force && _initialized)
                return;
            LoadingRelated = Visibility.Visible;
            _loadedRelated = true;
            RelatedAnime.Clear();
            var related = new List<RelatedAnimeData>();
            await Task.Run(async () => related = await new AnimeRelatedQuery(Id, _animeMode).GetRelatedAnime(force));
            if (related == null)
            {
                LoadingRelated = Visibility.Collapsed;
                NoRelatedDataNoticeVisibility = Visibility.Visible;
                return;
                
            }
            foreach (var item in related)
                RelatedAnime.Add(item);
            NoRelatedDataNoticeVisibility = RelatedAnime.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            LoadingRelated = Visibility.Collapsed;
        }

        #endregion

        /// <summary>
        /// Launches update of all UI bound variables.
        /// </summary>
        /// <param name="callerId">Anime item id that calls this thing.</param>
        public void UpdateAnimeReferenceUiBindings(int callerId)
        {
            if(callerId != Id)
                return;
            
            RaisePropertyChanged(() => StartDateTimeOffset);
            RaisePropertyChanged(() => EndDateTimeOffset);
            RaisePropertyChanged(() => MyEpisodesBind);
            RaisePropertyChanged(() => MyStatusBind);
            RaisePropertyChanged(() => MyScoreBind);
            RaisePropertyChanged(() => MyEpisodesBind);
            RaisePropertyChanged(() => MyStatusBind);
            RaisePropertyChanged(() => MyScoreBind);
            RaisePropertyChanged(() => MyStartDate);
            RaisePropertyChanged(() => MyEndDate);
            RaisePropertyChanged(() => IncrementEpsCommand);
            RaisePropertyChanged(() => DecrementEpsCommand);
            RaisePropertyChanged(() => IsIncrementButtonEnabled);
            RaisePropertyChanged(() => IsDecrementButtonEnabled);
        }

        private bool _loadingAlternate;
        private async Task<string> LoadHummingbirdCoverImage()
        {
            if (!_animeMode)
            {
                return null;
            }
            if (_loadingAlternate)
                return null;
            _loadingAlternate = true;
            LoadingUpdate = true;
            AnimeDetailsData data = null;
            await Task.Run(async () => data = await new AnimeDetailsHummingbirdQuery(Id).GetAnimeDetails());
            LoadingUpdate = false;
            _loadingAlternate = false;
            return data?.AlternateCoverImgUrl;
        }
    }
}