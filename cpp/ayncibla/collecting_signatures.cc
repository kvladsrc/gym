#include <algorithm>
#include <boost/range/algorithm/sort.hpp>
#include <climits>
#include <iostream>
#include <vector>

using std::vector;

struct Segment {
  int start, end;
};

static bool compareSegments(const Segment& a, const Segment& b) {
  return a.start > b.start;
}

static vector<int> optimal_points(vector<Segment>& segments) {
  vector<int> points;

  boost::range::sort(segments, compareSegments);

  for (const auto& segment : segments) {
    bool covered =
        std::any_of(points.begin(), points.end(), [&segment](int point) {
          return point >= segment.start && point <= segment.end;
        });

    if (!covered) {
      points.push_back(segment.start);
    }
  }

  return points;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<Segment> segments(n);
  for (auto& segment : segments) {
    std::cin >> segment.start >> segment.end;
  }
  vector<int> const points = optimal_points(segments);
  std::cout << points.size() << "\n";
  for (int const point : points) {
    std::cout << point << " ";
  }
}
