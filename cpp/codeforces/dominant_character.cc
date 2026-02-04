#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string s;
    std::cin >> s;

    // Result string is starts and ends by 'a'. If not we can trim it
    // to first 'a' from end and/or start. Possible result strings:
    // axa, axya, aa, axxayya. Lemma: that is all resulting strings.

    vector<int> prefix_a(n + 1);
    vector<int> prefix_b(n + 1);
    vector<int> prefix_c(n + 1);
    int res = -1;
    prefix_a.front() = prefix_b.front() = prefix_c.front() = 0;
    for (size_t idx = 0; idx < s.size(); ++idx) {
      prefix_a[idx + 1] = prefix_a[idx];
      prefix_b[idx + 1] = prefix_b[idx];
      prefix_c[idx + 1] = prefix_c[idx];
      switch (s[idx]) {
        case 'a':
          prefix_a[idx + 1]++;
          break;
        case 'b':
          prefix_b[idx + 1]++;
          break;
        default:
          prefix_c[idx + 1]++;
      }
    }

    for (size_t start = 0; start < s.size(); ++start) {
      if (s[start] != 'a') {
        continue;
      }

      for (int len = 2; len < 8; ++len) {
        if (start + len > s.size()) {
          break;
        }

        int a = prefix_a[start + len] - prefix_a[start];
        int b = prefix_b[start + len] - prefix_b[start];
        int c = prefix_c[start + len] - prefix_c[start];
        if (a > b && a > c) {
          if (res == -1) {
            res = len;
          } else {
            res = min(res, len);
          }
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
