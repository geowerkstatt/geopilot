import "./app.css";

export const Header = ({ clientSettings }) => (
  <header>
    {clientSettings?.vendor?.logo && (
      <a href={clientSettings?.vendor?.url} target="_blank" rel="noreferrer">
        <img
          className="vendor-logo"
          src={clientSettings?.vendor?.logo}
          alt={`Logo of ${clientSettings?.vendor?.name}`}
          onError={(e) => {
            e.target.style.display = "none";
          }}
        />
      </a>
    )}
  </header>
);

export default Header;
