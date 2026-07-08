import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate } from "react-router-dom";
import { ChevronLeft, ExpandMore } from "@mui/icons-material";
import { Accordion, AccordionDetails, AccordionSummary, Box, Link, Stack, Typography } from "@mui/material";
import { ContentType } from "../../api/apiInterfaces.ts";
import { Button } from "../../components/buttons.tsx";
import { CenteredContent } from "../../components/styledComponents.ts";
import useFetch from "../../hooks/useFetch.ts";

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
  const { fetchApi } = useFetch();
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
  }, [hash, licenseInfo, licenseInfoCustom]);

  return (
    <CenteredContent>
      <Box sx={{ flex: 0 }}>
        <Button id="backButton" variant="text" startIcon={<ChevronLeft />} onClick={() => navigate(-1)} label="back" />
      </Box>
      {(licenseInfo || licenseInfoCustom) && (
        <Typography variant="h1" id="licenses">
          {t("licenseInformation")}
        </Typography>
      )}
      <Stack gap={0}>
        {licenseGroups.map((group, index) => (
          <Accordion key={group.groupName + index} slotProps={{ transition: { timeout: 200 } }}>
            <AccordionSummary expandIcon={<ExpandMore />}>
              <Stack direction="row" sx={{ alignItems: "center" }}>
                <Typography variant="h4" m={0}>
                  {group.groupName}
                </Typography>
                <Typography>{group.packages.length > 1 ? `${group.packages.length} ${t("licenses")}` : ""}</Typography>
              </Stack>
            </AccordionSummary>
            <AccordionDetails>
              {group.packages.map(pkg => (
                <Stack key={pkg.name + pkg.version} gap={1}>
                  <Typography variant="h5">
                    {pkg.name}
                    {pkg.version && ` (${t("version")} ${pkg.version})`}
                  </Typography>
                  <Link href={pkg.repository}>{pkg.repository}</Link>
                  <Typography>{pkg.description}</Typography>
                  <Typography>{pkg.copyright}</Typography>
                  <Typography>
                    {t("licenses")}: {pkg.licenses}
                  </Typography>
                  <Typography>{pkg.licenseText}</Typography>
                </Stack>
              ))}
            </AccordionDetails>
          </Accordion>
        ))}
      </Stack>
    </CenteredContent>
  );
};
