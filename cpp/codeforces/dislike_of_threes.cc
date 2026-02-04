#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

vector<int> fill_table() {
  vector<int> dp(1000 + 1);
  int elem = 1;
  for (int i = 1; i <= 1000; ++i) {
    while (!(elem % 3) || elem % 10 == 3) {
      elem++;
    }
    dp[i] = elem++;
  }
  return dp;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  auto table = fill_table();
  while (t) {
    int k;
    std::cin >> k;
    std::cout << table[k] << "\n";
    t--;
  }

  return 0;
}
