use strict;
use File::Spec;
use File::Basename qw(dirname);
use File::Path;
use File::Copy;
use Getopt::Long;

my $mydir = File::Spec->rel2abs(dirname(__FILE__));
my $sln = "$mydir/SaferMutex.sln";

print "Solution path : $sln\n";
print "\nRunning test suite:\n";

if ($^O eq "MSWin32")
{
	system("$mydir/external/NUnit/nunit-console.exe", $sln) == 0 or die("test suite failed\n");
}
else
{
	die("Not Implemented\n");
}
