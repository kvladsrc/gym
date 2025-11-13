#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <utility>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  vector<pair<int, int>> inputs;
  for (int i = 0; i < t; ++i) {
    pair<int, int> buf;
    std::cin >> buf.first >> buf.second;
    inputs.push_back(buf);
  }
  for (auto p : inputs) {
    int d = abs(p.first - p.second);
    int answer = d / 10;
    if (d % 10) answer++;
    std::cout << answer << "\n";
  }

  return 0;
}
