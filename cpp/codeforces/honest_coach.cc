#include <algorithm>
#include <cstddef>
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

#define INF 1000000000;

int min_diff(vector<int>& athletes) {
  sort(athletes.begin(), athletes.end());
  int res = INF;
  for (size_t i = 1; i < athletes.size(); ++i) {
    if (abs(athletes[i] - athletes[i - 1]) < res)
      res = abs(athletes[i] - athletes[i - 1]);
  }
  return res;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> athletes(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> athletes[i];
    }
    std::cout << min_diff(athletes) << "\n";
  }
  return 0;
}
