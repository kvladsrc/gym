#include <iostream>
#include <vector>

using std::vector;

int dist(vector<int> heights, int h) {
  int res = 0;
  for (int a : heights) {
    if (a > h) {
      res += 2;
      continue;
    }
    res++;
  }
  return res;
}

int main(int argc, char *argv[]) {
  int n, h;
  std::cin >> n >> h;
  vector<int> heights;
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    heights.push_back(buf);
  }

  std::cout << dist(heights, h) << "\n";

  return 0;
}
