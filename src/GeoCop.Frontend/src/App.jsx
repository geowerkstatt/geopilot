import { Component } from "react";

export default class App extends Component {
  static displayName = App.name;

  constructor(props) {
    super(props);
    this.state = { version: "Unknown", loading: true };
  }

  componentDidMount() {
    this.fetchVersion();
  }

  static renderVersionInfo(version) {
    return <p>Current Version {version}</p>;
  }

  render() {
    let contents = this.state.loading ? (
      <p>
        <em>
          Loading... Please refresh once the ASP.NET backend has started. See{" "}
          <a href="https://aka.ms/jspsintegrationreact">
            https://aka.ms/jspsintegrationreact
          </a>{" "}
          for more details.
        </em>
      </p>
    ) : (
      App.renderVersionInfo(this.state.version)
    );

    return (
      <div>
        <h1 id="tabelLabel">geocop</h1>
        <p>This component demonstrates fetching data from the server.</p>
        {contents}
      </div>
    );
  }

  async fetchVersion() {
    const response = await fetch("api/Version");
    const data = await response.text();
    if (data.length == 0) return;
    this.setState({ version: data, loading: false });
  }
}
