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
    string s;
    std::cin >> s;

    int n = 0;
    std::cin >> n;

    if (n <= static_cast<int>(s.size())) {
      std::cout << s[n - 1];
      continue;
    }

    int cur_size = s.size();
    while ((n - cur_size) > 0) {
      n -= cur_size--;
    }

    vector<char> st;
    int need_to_remove = s.size() - cur_size;
    for (char i : s) {
      while (!st.empty() && i < st.back() && need_to_remove > 0) {
        st.pop_back();
        need_to_remove--;
      }

      st.push_back(i);
    }

    std::cout << st[n - 1];
  }
  std::cout << "\n";

  return 0;
}
