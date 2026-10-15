export default function (view) {
    const pluginId = 'a7fcd5d8-0879-48d1-96f3-b802a9a91128';
    const taskKey = 'DateAddedToDateAired.ApplyReleaseAirDates';

    const setStatus = function (message) {
        view.querySelector('#configStatus').textContent = message;
    };

    const setting = function (configuration, pascalCaseName) {
        const camelCaseName = pascalCaseName.charAt(0).toLowerCase() + pascalCaseName.slice(1);
        return configuration[pascalCaseName] === true || configuration[camelCaseName] === true;
    };

    const load = function () {
        Dashboard.showLoadingMsg();
        setStatus('Loading settings…');

        return ApiClient.getPluginConfiguration(pluginId).then(function (configuration) {
            view.querySelector('#enableMovies').checked = setting(configuration, 'EnableMovies');
            view.querySelector('#enableEpisodes').checked = setting(configuration, 'EnableTvEpisodes');
            view.querySelector('#automatic').checked = setting(configuration, 'ProcessNewItemsAutomatically');
            view.querySelector('#skipFuture').checked = setting(configuration, 'SkipFuturePremiereDates');
            setStatus('Settings loaded.');
        }).catch(function (error) {
            console.error('Unable to load Date Added To Date Aired configuration.', error);
            setStatus('Unable to load settings: ' + error);
        }).finally(function () {
            Dashboard.hideLoadingMsg();
        });
    };

    view.addEventListener('viewshow', load);

    view.querySelector('#DateAddedToDateAiredConfigForm').addEventListener('submit', function (event) {
        event.preventDefault();
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then(function (configuration) {
            configuration.EnableMovies = view.querySelector('#enableMovies').checked;
            configuration.EnableTvEpisodes = view.querySelector('#enableEpisodes').checked;
            configuration.ProcessNewItemsAutomatically = view.querySelector('#automatic').checked;
            configuration.SkipFuturePremiereDates = view.querySelector('#skipFuture').checked;
            return ApiClient.updatePluginConfiguration(pluginId, configuration);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
            setStatus('Settings saved.');
            return load();
        }).catch(function (error) {
            console.error('Unable to save Date Added To Date Aired configuration.', error);
            Dashboard.alert('Unable to save Date Added To Date Aired settings. Check the Jellyfin server log.');
        }).finally(function () {
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#runNow').addEventListener('click', function () {
        Dashboard.showLoadingMsg();

        ApiClient.getScheduledTasks().then(function (tasks) {
            const task = tasks.find(function (candidate) {
                return candidate.Key === taskKey || candidate.key === taskKey;
            });

            if (!task) {
                throw new Error('The Date Added task is unavailable. Restart Jellyfin and try again.');
            }

            return ApiClient.startScheduledTask(task.Id || task.id);
        }).then(function () {
            Dashboard.alert('Date Added update task started. Monitor the DateAddedToDateAired log for progress.');
        }).catch(function (error) {
            console.error('Unable to start Date Added update task.', error);
            Dashboard.alert('Unable to start the Date Added update task. Check the Jellyfin server log.');
        }).finally(function () {
            Dashboard.hideLoadingMsg();
        });
    });
}
