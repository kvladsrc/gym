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
    string s;
    std::cin >> s;
    string pat;
    std::cin >> pat;

    size_t pat_idx = 0;
    for (auto& c : s) {
      if (pat_idx >= pat.size()) {
        break;
      }

      if (pat[pat_idx] == c) {
        pat_idx++;
        continue;
      }

      if (c == '?') {
        c = pat[pat_idx++];
      }
    }

    if (pat_idx != pat.size()) {
      std::cout << "NO" << "\n";
      continue;
    }

    std::cout << "YES" << "\n";
    std::replace_if(s.begin(), s.end(), [](char c) { return c == '?'; }, 'a');

    std::cout << s << "\n";
  }

  return 0;
}
