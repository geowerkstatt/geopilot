interface PackageList {
  [packageName: string]: PackageDetails;
}

interface PackageDetails {
  licenses?: string;
  repository?: string;
  publisher?: string;
  email?: string;
  url?: string;
  name: string;
  version: string;
  description?: string;
  copyright?: string;
  licenseText?: string;
  path?: string;
  licenseFile?: string;
}

export const Licenses = () => {
  const { t } = useTranslation();
  const [licenseInfo, setLicenseInfo] = useState<PackageList>();
  const [licenseInfoCustom, setLicenseInfoCustom] = useState<PackageList>();
  const { fetchApi } = useApi();
  const { hash } = useLocation();

  useEffect(() => {
    fetchApi<PackageList>("/license.json", { responseType: ContentType.Json }).then(setLicenseInfo);
    fetchApi<PackageList>("/license.custom.json", { responseType: ContentType.Json }).then(setLicenseInfoCustom);
  }, [fetchApi]);

  useEffect(() => {
    const scrollToHash = () => {
      if (hash) {
        const id = hash.substring(1);
        const element = document.getElementById(id);
        if (element) window.scrollTo({ top: element.offsetTop - 64, behavior: "smooth" });
      }
    };

    // Run after initial render
    setTimeout(scrollToHash, 0);
    scrollToHash();
  }, [hash, licenseInfo, licenseInfoCustom]);

  return (
    <CenteredBox>

      </FlexRowSpaceBetweenBox>
      {(licenseInfo || licenseInfoCustom) && (
        <Typography variant="h1" id="licenses">
          {t("licenseInformation")}
        </Typography>
      )}
      
    </CenteredBox>
  );
};
