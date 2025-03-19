import { useTranslation } from "react-i18next";
import { useEffect, useMemo, useState } from "react";
import { useApi } from "../../api";
import { Accordion, AccordionDetails, AccordionSummary, Box, Grid, Link, Typography } from "@mui/material";
import { ContentType } from "../../api/apiInterfaces.ts";
import { CenteredBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { useLocation, useNavigate } from "react-router-dom";
import { ChevronLeft, ExpandMore } from "@mui/icons-material";
import { BaseButton } from "../../components/buttons.tsx";

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

interface PackageGroup {
  groupName: string;
  packages: PackageDetails[];
}

export const Licenses = () => {
  const { t } = useTranslation();
  const [licenseInfo, setLicenseInfo] = useState<PackageList>();
  const [licenseInfoCustom, setLicenseInfoCustom] = useState<PackageList>();
  const { fetchApi } = useApi();
  const { hash } = useLocation();
  const navigate = useNavigate();

  const addPackageToGroup = (groups: PackageGroup[], packageName: string, details: PackageDetails): void => {
    const groupName = details.publisher || packageName.split("/")[0];
    const existingGroup = groups.find(group => group.groupName === groupName);

    if (existingGroup) {
      existingGroup.packages.push(details);
    } else {
      groups.push({
        groupName,
        packages: [details],
      });
    }
  };

  const licenseGroups = useMemo(() => {
    const groups: PackageGroup[] = [];

    if (licenseInfoCustom) {
      Object.entries(licenseInfoCustom).forEach(([packageName, details]) => {
        addPackageToGroup(groups, packageName, details);
      });
    }

    if (licenseInfo) {
      Object.entries(licenseInfo).forEach(([packageName, details]) => {
        addPackageToGroup(groups, packageName, details);
      });
    }

    // Sort groups alphabetically
    return groups.sort((a, b) => a.groupName.localeCompare(b.groupName));
  }, [licenseInfo, licenseInfoCustom]);

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
      <FlexRowSpaceBetweenBox>
        <BaseButton
          id="backButton"
          variant={"text"}
          color="primary"
          icon={<ChevronLeft />}
          onClick={() => navigate(-1)}
          label="back"
        />
      </FlexRowSpaceBetweenBox>
      {(licenseInfo || licenseInfoCustom) && (
        <Typography variant="h1" id="licenses">
          {t("licenseInformation")}
        </Typography>
      )}
      <>
        {licenseGroups.map(group => (
          <Accordion key={group.groupName} slotProps={{ transition: { timeout: 200 } }}>
            <AccordionSummary expandIcon={<ExpandMore />}>
              <Grid container spacing={1}>
                <Grid item xs={12}>
                  <Typography variant="h2">{group.groupName}</Typography>
                </Grid>
                <Grid item xs={12}>
                  <Typography>
                    {group.packages.length > 1 ? `${group.packages.length} ${t("licenses")}` : ""}
                  </Typography>
                </Grid>
              </Grid>
            </AccordionSummary>
            <AccordionDetails>
              {group.packages.map(pkg => (
                <Box key={pkg.name} sx={{ py: 4 }}>
                  <Typography variant="h3">
                    {pkg.name}
                    {pkg.version && ` (${t("version")} ${pkg.version})`}{" "}
                  </Typography>
                  <p>
                    <Link href={pkg.repository}>{pkg.repository}</Link>
                  </p>
                  <p>{pkg.description}</p>
                  <p>{pkg.copyright}</p>
                  <p>
                    {t("licenses")}: {pkg.licenses}
                  </p>
                  <p>{pkg.licenseText}</p>
                </Box>
              ))}
            </AccordionDetails>
          </Accordion>
        ))}
      </>
    </CenteredBox>
  );
};
