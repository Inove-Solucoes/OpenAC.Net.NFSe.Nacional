using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace OpenAC.Net.NFSe.Nacional
{
    /// <summary>
    /// Classe estática com funções e artefatos de auxílio para o processamento de NFSe, como montagem de envelopes SOAP, manipulação de XML, etc.
    /// </summary>
    public static class Uteis // Lucas Ticket: #16665 13/03/2026
    {
        /// <summary>
        /// Função para montagem do envelope SOAP, utilizando o XML de envio e a operação SOAP como parâmetros.
        /// </summary>
        /// <param name="xmlEnvio">O XML de envio a ser encapsulado no envelope SOAP.</param>
        /// <param name="operacaoSoap">A operação SOAP a ser chamada (ex: "RecepcionarLoteRps).</param>

        public static string MontarSoap(string xmlEnvio, string operacaoSoap)
        {
            return
$@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:web=""http://webservices.sil.com/"">
    <soapenv:Header/>
    <soapenv:Body>
        <web:{operacaoSoap}>
            <xml><![CDATA[{xmlEnvio}]]></xml>
        </web:{operacaoSoap}>
    </soapenv:Body>
</soapenv:Envelope>";
        }

        /// <summary>
        /// Função para assinar e preencher a tag <signature> do XML de NFSe, utilizando o campo infNFSe como referência para a assinatura. O certificado é obtido a partir da configuração do service provider.
        /// </summary>
        /// <param name="xmlNfse">XML da NFSe com o campo infNFSe preenchido</param>
        /// <param name="configuracaoNFSe">Configuração do provider, utilizado para obter o certificado de assinatura</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string AssinarUsandoOCampoInfNFSe(string xmlNfse, ConfiguracaoNFSe configuracaoNFSe)
        {
            var xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };
            xmlDoc.LoadXml(xmlNfse);

            var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("nfse", "http://www.sped.fazenda.gov.br/nfse");

            var infNFSeNode = xmlDoc.SelectSingleNode("//nfse:infNFSe", nsMgr) as XmlElement ?? throw new Exception("Elemento infNFSe não encontrado para assinatura.");
            var id = infNFSeNode.GetAttribute("Id");
            if (string.IsNullOrWhiteSpace(id))
                throw new Exception("Atributo Id do infNFSe não encontrado.");

            var certificado = configuracaoNFSe.Certificados.ObterCertificado() ?? throw new Exception("Certificado não encontrado para assinatura.");
            var signedXml = new SignedXml(xmlDoc)
            {
                SigningKey = certificado.GetRSAPrivateKey()
            };

            var reference = new Reference
            {
                Uri = "#" + id
            };

            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigC14NTransform());
            reference.DigestMethod = SignedXml.XmlDsigSHA1Url;

            signedXml.AddReference(reference);

            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
            signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();

            var xmlSignature = signedXml.GetXml();

            // insere a assinatura como filha de NFSe, após infNFSe
            var nfseNode = xmlDoc.DocumentElement ?? throw new Exception("Elemento NFSe não encontrado.");

            var importedSignature = xmlDoc.ImportNode(xmlSignature, true);
            nfseNode.AppendChild(importedSignature);

            return xmlDoc.OuterXml;
        }

        /// <summary>
        /// Gera o XML do DPS sem a assinatura, pois o OpenAC somente gera o XML ao chamar a função de assinar.
        /// </summary>
        /// <param name="dps">DPS da nota fiscal.</param>
        /// <returns></returns>
        public static string GerarXmlDpsSemAssinatura(Dps dps)
        {
            using var ms = new MemoryStream();

            var serializer = DFeSerializer<Dps>.CreateSerializer<Dps>();
            serializer.Serialize(dps, ms);

            ms.Position = 0;

            using var sr = new StreamReader(ms, Encoding.UTF8);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Calcula o dígito verificador utilizando o método do Módulo 11.
        /// </summary>
        /// <param name="valor">Número base para o calculo.</param>
        /// <returns></returns>
        public static int CalcularDvModulo11(string valor)
        {
            var apenasDigitos = valor.Substring(4).ToArray();

            var soma = 0;
            var peso = 2;

            for (int i = apenasDigitos.Length - 1; i >= 0; i--)
            {
                soma += (apenasDigitos[i] - '0') * peso;
                peso++;

                if (peso > 9)
                    peso = 2;
            }

            var resto = soma % 11;

            if (resto == 0 || resto == 1 || resto == 10)
                return 0;

            return 11 - resto;
        }

        /// <summary>
        /// Função auxiliar para transformar um valor decimal em string com 2 casas decimais e utilizando ponto como separador decimal, conforme exigido pelo layout da NFSe.
        /// </summary>
        /// <param name="valor"></param>
        /// <returns></returns>
        public static string FormatarValorPadraoNFSe(decimal valor) =>
            valor.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>
        /// Função auxiliar para extrair somente os dígitos de uma string, removendo quaisquer caracteres não numéricos. Útil para limpar campos como CPF, CNPJ, CEP, etc., antes de processá-los ou armazená-los.
        /// </summary>
        /// <param name="valor">String que se deseja extrair os números.</param>
        /// <returns></returns>
        public static string SomenteDigitos(string valor) =>
            new((valor ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
