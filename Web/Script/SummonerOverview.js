function getOverviewTable(fields)
{
    var output = table();
    output.className = 'summonerOverview';
    for(var i in fields)
    {
        var entry = fields[i];
        var description = entry[0];
        var value = entry[1];

        var row = tableRow();
        row.add(tableCell(bold(description)));
        row.add(tableCell(value));
        output.add(row);
    }

    return output;
}

function getAutomaticUpdateDescription(container, region, summoner)
{
    var output =
        [
            (summoner.UpdateAutomatically ? 'Yes' : 'No') + ' (',
            anchor(summoner.UpdateAutomatically ? 'disable' : 'enable', function() { setAutomaticUpdates(container, region, summoner, !summoner.UpdateAutomatically); } ),
            ')',
        ];

    return output;
}

function getSummonerOverview(summoner, statistics)
{
    var ratings = statistics.Ratings;

    var region = system.regions[summoner.Region].abbreviation;

    var profileIcon = image('Profile/profileIcon' + summoner.ProfileIcon + '.jpg', summoner.SummonerName + "'s profile icon", 128, 128);
    profileIcon.id = 'profileIcon';

    var gamesPlayed = 0;
    for(i in ratings)
    {
        var statistics = ratings[i];
        gamesPlayed += statistics.Wins;
        gamesPlayed += statistics.Losses;
    }

    var overviewFields1 =
        [
            ['Summoner name', summoner.SummonerName],
            ['Internal name', summoner.InternalName],
            ['Region', region],
            ['Summoner level', summoner.SummonerLevel],
            ['Non-custom games played', gamesPlayed],
            ['Account ID', summoner.AccountId],
            ['Summoner ID', summoner.SummonerId],
        ];

    var matchHistoryLink = anchor('View games', function() { viewMatchHistory(region, summoner.AccountId); } );

    var overviewFields2 =
        [
            ['Match history', matchHistoryLink],
        ];

    var updateDescription = 'Is updated automatically';
    if(system.privileged)
    {
        //Requesting updates requires writing permissions
        var manualUpdateContainer = span();
        manualUpdateContainer.add(anchor('Update now', function() { updateSummoner(manualUpdateContainer, region, summoner.AccountId); } ));
        overviewFields2.push(['Manual update', manualUpdateContainer]);
        var automaticUpdateContainer = span();
        automaticUpdateContainer.add(getAutomaticUpdateDescription(automaticUpdateContainer, region, summoner));
        overviewFields2.push([updateDescription, automaticUpdateContainer]);
    }
    else
        overviewFields2.push([updateDescription, summoner.UpdateAutomatically ? 'Yes' : 'No']);

    overviewFields2 = overviewFields2.concat
    (
        [
            ['First update', getTimestampString(summoner.TimeCreated)],
            ['Last update', getTimestampString(summoner.TimeUpdated)],
        ]
    );

    var container = diverse();
    container.id = 'summonerHeader';
    container.add(profileIcon);
    container.add(getOverviewTable(overviewFields1));
    container.add(getOverviewTable(overviewFields2));

    return container;
}