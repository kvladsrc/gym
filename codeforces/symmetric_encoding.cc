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

    string b;
    std::cin >> b;

    map<char, char> m;
    for (auto c : b) {
      m[c] = c;
    }

    vector<char> table;
    table.reserve(m.size());
    for (auto p : m) {
      table.push_back(p.first);
    }

    for (size_t idx = 0; idx < table.size(); ++idx) {
      m[table[idx]] = table[(table.size() - 1) - idx];
    }

    string s;
    for (auto c : b) {
      s.push_back(m[c]);
    }

    std::cout << s << "\n";
  }

  return 0;
}
