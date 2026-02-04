#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void erato_fill(vector<bool>& table) {
  for (size_t i = 2; i < table.size(); ++i) {
    if (table[i]) continue;
    size_t next = i + i;
    while (next < table.size()) {
      table[next] = true;
      next += i;
    }
  }
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<bool> erato(n / 2 + 1);
  erato_fill(erato);

  int res = 0;
  for (int i = 1; i <= n; ++i) {
    unordered_map<int, bool> m;
    for (int j = 2; j <= (n / 2); ++j) {
      if (erato[j]) continue;
      if (i % j == 0) {
        m[j] = true;
      }
    }
    if (m.size() == 2) res++;
  }

  std::cout << res << "\n";

  return 0;
}
