#include <sys/types.h>

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

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    char cur;
    std::cin >> cur;

    string s;
    std::cin >> s;
    s += s;

    if (cur == 'g') {
      std::cout << 0 << "\n";
      continue;
    }

    int global = INT_MIN;
    ssize_t last = -1;
    for (ssize_t idx = 0; idx < static_cast<ssize_t>(s.size()); ++idx) {
      if (s[idx] == cur && last == -1) {
        last = idx;
        continue;
      }

      if (s[idx] == 'g' && last != -1) {
        global = max(global, static_cast<int>(idx - last));
        last = -1;
      }
    }

    std::cout << global << "\n";
  }

  return 0;
}
