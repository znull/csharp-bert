BUILD_CONFIG = Debug
BERT_CODEC_TEST_DLL = tests/BertCodec.Tests/bin/$(BUILD_CONFIG)/BertCodec.Tests.dll
NUNIT_OPTS = -noresult -nologo -stoponerror -labels

test:
	xbuild src/BertCodec/BertCodec.csproj
	xbuild tests/BertCodec.Tests/BertCodec.Tests.csproj

	nunit-console $(BERT_CODEC_TEST_DLL) $(NUNIT_OPTS)

clean:
	rm -rf src/*/bin
	rm -rf src/*/obj
	rm -rf tests/*/bin
	rm -rf tests/*/obj
