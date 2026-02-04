#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    string s;
    std::cin >> s;

    // Better option is to count chars when s[i - 1] != s[i + 1].
    char prev_prev = s[0] + 1;
    char prev = s[1] + 1;
    int num_rep_segs = 0;
    for (auto c : s) {
      if (!(c == prev_prev)) {
        num_rep_segs++;
      }
      prev_prev = prev;
      prev = c;
    }

    //   abac...c
    // a  bac...c
    // ab  ac...c
    // aba  c...c
    // ----------
    // abab  ...c

    // deleting any two cons. chars from repeating segment cause the
    // same result.
    //
    // result: num_repeating_segments + number_of pairs between the
    // segments. (wrong. How about rest chars?)
    int res = num_rep_segs - 1;
    std::cout << res << "\n";
  }

  return 0;
}
