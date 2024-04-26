export const About = ({ clientSettings, licenseInfo, appVersion }) => (
  <div>
    <h1>About</h1>
    <h2>Versionsinformationen</h2>
    <p>
      <b>{clientSettings?.application?.name}</b>: {appVersion}
    </p>
    <h2>Entwicklung und Bugtracking</h2>
    <p>
      Der Code steht unter der{" "}
      <a href="https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE" target="_blank" rel="noopener noreferrer">
        MIT License
      </a>{" "}
      im{" "}
      <a href="https://github.com/GeoWerkstatt/geopilot" target="_blank" rel="noopener noreferrer">
        GitHub Repository
      </a>{" "}
      zur Verfügung. Falls Ihnen Bugs begegnen, können Sie dort ein{" "}
      <a href="https://github.com/GeoWerkstatt/geopilot/issues/" target="_blank" rel="noopener noreferrer">
        Issue
      </a>{" "}
      eröffnen.
    </p>
    <h2>Lizenzinformationen</h2>
    {Object.keys(licenseInfo).map(key => (
      <div key={key} className="about-licenses">
        <h3>
          {licenseInfo[key].name}
          {licenseInfo[key].version && ` (Version ${licenseInfo[key].version})`}{" "}
        </h3>
        <p>
          <a href={licenseInfo[key].repository}>{licenseInfo[key].repository}</a>
        </p>
        <p>{licenseInfo[key].description}</p>
        <p>{licenseInfo[key].copyright}</p>
        <p>License: {licenseInfo[key].licenses}</p>
        <p>{licenseInfo[key].licenseText}</p>
      </div>
    ))}
  </div>
);

export default About;
