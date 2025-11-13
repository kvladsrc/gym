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

  int n = 0;
  std::cin >> n;

  string s;
  std::cin >> s;

  bool end = true;

  string res(s);
  size_t l = 0;
  size_t r = s.size() - 1;
  std::reverse(s.begin(), s.end());
  for (char c : s) {
    if (end) {
      res[r--] = c;
    } else {
      res[l++] = c;
    }
    end = !end;
  }
  std::cout << res << "\n";

  return 0;
}
