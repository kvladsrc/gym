#include <algorithm>
#include <cstddef>
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

int find_spy(const vector<int>& input) {
  if (input[0] != input[1] && input[0] != input[2]) return 1;
  if (input[input.size() - 1] != input[input.size() - 2] &&
      input[input.size() - 1] != input[input.size() - 3])
    return input.size();

  for (size_t i = 1; i < input.size() - 1; ++i) {
    if (input[i] != input[i - 1] && input[i] != input[i + 1]) {
      return i + 1;
    }
  }
  return 0;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  vector<vector<int>> inputs(t);
  for (auto& v : inputs) {
    int n;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      v.push_back(buf);
    }
  }

  for (const auto& v : inputs) {
    std::cout << find_spy(v) << "\n";
  }

  return 0;
}
