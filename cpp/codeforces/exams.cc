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
#include <utility>
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

bool compare_pair(const pair<int, int>& a, const pair<int, int>& b) {
  if (a.first == b.first) {
    return a.second < b.second;
  }

  return a.first < b.first;
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;

  vector<pair<int, int>> schedule(n);

  for (auto& p : schedule) {
    std::cin >> p.first;
    std::cin >> p.second;
  }

  sort(schedule.begin(), schedule.end(), compare_pair);
  int day = 1;
  for (auto p : schedule) {
    if (p.second < day) {
      day = p.first;
      continue;
    }

    day = p.second;
  }

  std::cout << day << "\n";

  return 0;
}
