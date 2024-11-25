#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    vector<int> pref_sums(s.size() + 1);
    pref_sums[0] = 0;
    for (size_t i = 0; i < s.size(); ++i) {
      if (s[i] == '1') {
        pref_sums[i + 1] = pref_sums[i] + 1;
      } else {
        pref_sums[i + 1] = pref_sums[i];
      }
    }

    int case_all_zeroes = pref_sums.back();
    int case_all_ones = s.size() - pref_sums.back();

    // Best of 0...0 and 1...1.
    int best = min(case_all_ones, case_all_zeroes);

    for (size_t i = 0; i < s.size(); ++i) {
      // 0...i1...1
      int inc = pref_sums[i + 1]; // Swap all ones 0...i.
      inc += (s.size() - (i + 1)) -
             (pref_sums.back() -
              pref_sums[i + 1]); // Swap all zeroes i + 1...size;

      // 1...i0...0
      int dec = (i + 1) - pref_sums[i + 1]; // Swal all zeroes 0...i.
      dec += pref_sums.back() - pref_sums[i + 1];

      auto local_min = min(inc, dec);
      if (local_min < best) {
        best = local_min;
      }
    }

    std::cout << best << "\n";
  }

  return 0;
}
