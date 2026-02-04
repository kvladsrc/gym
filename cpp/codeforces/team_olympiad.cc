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

int main(int argc, char* argv[]) {
  vector<stack<int>> teams(3);
  int n;
  std::cin >> n;
  for (int i = 0; i < n; ++i) {
    int t;
    std::cin >> t;
    teams[t - 1].push(i + 1);
  }

  int teams_count = min(min(teams[0].size(), teams[1].size()), teams[2].size());
  std::cout << teams_count << "\n";

  for (int i = 0; i < teams_count; ++i) {
    std::cout << teams[0].top() << " " << teams[1].top() << " "
              << teams[2].top() << "\n";
    teams[0].pop();
    teams[1].pop();
    teams[2].pop();
  }

  return 0;
}
