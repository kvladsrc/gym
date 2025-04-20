#include <algorithm>
#include <climits>
#include <cmath>
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

int ctoi(char c) { return c - '0'; }

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    string s;
    std::cin >> s;

    string res;
    queue<int> odd;
    queue<int> even;

    for (auto c : s) {
      auto i = ctoi(c);

      if (i % 2 == 0) {
        even.push(i);
      } else {
        odd.push(i);
      }
    }

    while (!even.empty() || !odd.empty()) {
      if (even.empty()) {
        res += std::to_string(odd.front());
        odd.pop();
        continue;
      }

      if (odd.empty()) {
        res += std::to_string(even.front());
        even.pop();
        continue;
      }

      if (ctoi(odd.front()) <= ctoi(even.front())) {
        res += std::to_string(odd.front());
        odd.pop();
        continue;
      } else {
        res += std::to_string(even.front());
        even.pop();
        continue;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
