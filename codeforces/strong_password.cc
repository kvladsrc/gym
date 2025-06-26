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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    string s;
    std::cin >> s;

    if (s.size() == 1) {
      if (s.front() == 'a') {
        s.push_back('b');
      } else {
        s.push_back('a');
      }

      std::cout << s << "\n";
      continue;
    }

    bool done = false;
    for (size_t idx = 1; idx < s.size(); ++idx) {
      if (s[idx] == s[idx - 1]) {
        s.insert(idx, 1, (s[idx] == 'a' ? 'b' : 'a'));
        done = true;
        break;
      }
    }

    if (!done) {
      s.push_back((s.back() == 'a' ? 'b' : 'a'));
    }

    std::cout << s << "\n";
  }

  return 0;
}
