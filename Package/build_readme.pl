
use strict;
use Text::Template;

sub usage_and_exit() {
  print STDERR <<_HELP_;
PURPOSE:
  Build a file <x> from <x>.template and <x>.params.

USAGE:
  $0 template_file param_file > output_file

ARGUMENT:
 * template_file  a text file in Perl's Text::Template syntax (dynamic content
                  bracketed in '<%' and '%>')
 * param_file     a text file containing key/values for substition of the 
                  template file. It must eval'ed to a Perl hash-ref.

EXAMPLE:
 $0 ..\\README.md.src params.txt > ..\\README.md
_HELP_
  exit 2;
}

@ARGV==2 or usage_and_exit;

my $filename_tpl   = $ARGV[0];	-r $filename_tpl or die "File '$filename_tpl' does not exist\n";
my $filename_param = $ARGV[1];	-r $filename_param or die "File '$filename_param' does not exist\n";

my $params = do $filename_param; ref($params) eq 'HASH' or die "Invalid parameter file. Expect hash-ref content.\n";
my $template = Text::Template->new(
  SOURCE     => $filename_tpl,
  DELIMITERS => ['<%', '%>'],
);
$template->compile() or die "Invalid template file.";
$template->fill_in('HASH' => $params, OUTPUT => \*STDOUT);

