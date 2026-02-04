#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> cube(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> cube[i];
  }
  std::sort(cube.begin(), cube.end());
  for (int i = 0; i < n; ++i) {
    std::cout << cube[i] << " ";
  }

  return 0;
}
