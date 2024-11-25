#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  vector<int> tv(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> tv[i];
  }
  sort(tv.begin(), tv.end());
  int sum = 0, j = 0;
  while (m--) {
    if (tv[j] <= 0) {
      sum += tv[j++];
    }
  }
  std::cout << -sum << "\n";

  return 0;
}
