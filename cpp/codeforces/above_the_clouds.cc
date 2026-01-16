#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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
    int n = 0;
    std::cin >> n;

    string s;
    std::cin >> s;

    map<char, int> m;
    bool res = false;

    for (size_t i = 0; i < s.size(); ++i) {
      m[s[i]]++;
      if (m[s[i]] > 1) {
        if (m[s[i]] == 2 && i == s.size() - 1 && s.back() == s.front()) {
          continue;
        }
        res = res || m[s[i]] > 1;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
